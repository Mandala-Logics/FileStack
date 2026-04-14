using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using System.Threading;
using MandalaLogics.Encoding;
using MandalaLogics.Packing;

namespace MandalaLogics.Database
{
    /// <summary>
    /// Twine is a stream-backed hash-set.
    /// </summary>
    /// <typeparam name="T">Must be a reference-type IEncodable.</typeparam>
    public sealed partial class Twine<T> : ICollection<T> where T : class, IEncodable
    {
        private const int BlockSize = 512;
        private const int MaxChainLength = 48;
        private const int MaxCacheSize = 128 * 1024;
        private const ulong BucketCount = 256;
        
        private static readonly int BlockCapacity;
        
        public bool Disposed { get; private set; } = false;
        public int Count => _header.ChainCount;
        public bool IsReadOnly => false;
        
        private readonly Stream _stream;
        private readonly TwineHeader _header;
        private readonly List<TwineBlockHeader> _blocks;
        
        private readonly int _maxCacheSize;
        private int _cacheSize = 0;
        
        private readonly SemaphoreSlim _streamLock = new SemaphoreSlim(1);
        private readonly SemaphoreSlim _cacheLock = new SemaphoreSlim(1);
        private readonly SemaphoreSlim _bucketLock = new SemaphoreSlim(1);

        private readonly List<TwineEntry>?[] _buckets;
        
        static Twine()
        {
            EncodingRegister.RegisterAll(Assembly.GetAssembly(typeof(Twine<>)));

            BlockCapacity = BlockSize - TwineBlockHeader.EncodedSize;
        }

        public Twine(Stream stream, int maxCacheSize = MaxCacheSize)
        {
            if (maxCacheSize < 0) _maxCacheSize = 0;
            else if (maxCacheSize > MaxCacheSize) _maxCacheSize = MaxCacheSize;
            else _maxCacheSize = maxCacheSize;
            
            stream.Seek(0L, SeekOrigin.Begin);

            _stream = stream;
            
            try
            {
                EncodedValue.Read(stream, out var ev);

                if (ev.Value is TwineHeader th)
                {
                    _header = th;
                }
                else
                {
                    throw new TwineNotValidException("Failed to decode header.");
                }

                _header.CompareType(typeof(T));
            }
            catch (Exception e) when (e is EncodingException)
            {
                throw new TwineNotValidException("Twine not valid, could not read header.", e);
            }
            catch (EndOfStreamException) //stream is empty, probably
            {
                if (_stream.Length != 0) throw new TwineNotValidException("Failed to decode header.");

                _header = new TwineHeader(typeof(T));
                _header.WriteSelf(_stream);
                
                stream.SetLength(BlockSize);
            }

            _blocks = new List<TwineBlockHeader>(_header.BlockCount);

            _buckets = new List<TwineEntry>[BucketCount];
            
            ReadAllBlocks();
        }
        
        private void ReadAllBlocks()
        {
            try
            {
                var n = 1;

                do
                {
                    var pos = BlockSize * n;

                    _stream.Seek(pos, SeekOrigin.Begin);

                    EncodedValue.Read(_stream, out var ev);

                    if (ev.Value is TwineChainHeader ch)
                    {
                        ch.SetIndex(n - 1);
                        _blocks.Add(ch);

                        var bucket = (int)(ch.Hash % BucketCount);

                        _buckets[bucket] ??= new List<TwineEntry>();
                        
                        _buckets[bucket]!.Add(new TwineEntry(this, ch));
                    }
                    else if (ev.Value is TwineBlockHeader bh)
                    {
                        bh.SetIndex(n - 1);
                        _blocks.Add(bh);
                    }
                    else
                    {
                        throw new TwineNotValidException("Failed to decode blocks, unexpected value read.");
                    }
                    
                    n++;

                } while (true);
            }
            catch (EndOfStreamException) {}
            catch (Exception e) when (e is EncodingException)
            {
                throw new TwineNotValidException("Failed to decode blocks", e);
            }
        }

        private int GetFreeBlock(TwineBlockHeader header)
        {
            for (var x = 0; x < _blocks.Count; x++)
            {
                if (_blocks[x].Disused)
                {
                    _blocks[x] = header;
                    _blocks[x].SetIndex(x);
                    WriteBlock(x);
                    return x;
                }
            }

            return CreateBlock(header);
        }

        private int CreateBlock(TwineBlockHeader block)
        {
            int index = _blocks.Count;
                
            _stream.SetLength(_stream.Length + BlockSize);
            
            block.SetIndex(index);
            _blocks.Add(block);

            _header.BlockCount++;
            _header.WriteSelf(_stream);
            
            _stream.Seek((index + 1) * BlockSize, SeekOrigin.Begin);

            block.Encode().Write(_stream);

            return index;
        }

        private void WriteBlock(int blockIndex)
        {
            _stream.Seek((blockIndex + 1) * BlockSize, SeekOrigin.Begin);

            _blocks[blockIndex].Encode().Write(_stream);
        }
        
        private Stitch GetStitch(int index) => new Stitch((index + 1) * BlockSize + TwineBlockHeader.EncodedSize, BlockSize - TwineBlockHeader.EncodedSize);

        public void Dispose()
        {
            if (Disposed) return;

            _bucketLock.Wait();

            Disposed = true;
            
            _stream.Flush();
            _stream.Dispose();

            _bucketLock.Release();
        }
        
        void ICollection<T>.Add(T item) => Add(item);

        public bool Add(T item)
        {
            if (Disposed) throw new ObjectDisposedException(nameof(Twine<T>));
            
            _bucketLock.Wait();
            
            try
            {
                var eo = item.Encode();

                var hash = eo.GetLongHash();

                var bucket = (int)(hash % BucketCount);

                if (_buckets[bucket] is { })
                {
                    foreach (var cacheEntry in _buckets[bucket]!)
                    {
                        if (cacheEntry.Header.Hash == hash && item.Equals(cacheEntry.Get()))
                            return false;
                    }
                }
                else
                {
                    _buckets[bucket] = new List<TwineEntry>();
                }

                _streamLock.Wait();

                TwineChainHeader header;
                MemoryStream ms;

                try
                {
                    header = new TwineChainHeader(hash) { Disused = false };
                    
                    ms = eo.WriteToMemoryStream();

                    var blocks = Math.DivRem((int)ms.Length, BlockCapacity, out var rem) + (rem > 0 ? 1 : 0);

                    if (blocks > MaxChainLength)
                        throw new ArgumentException("Cannot encoded this value into Twine as it is too large.");

                    GetFreeBlock(header);
                    header.Disused = false;

                    var buffer = new byte[BlockCapacity];
                    ms.Position = 0L;

                    for (var x = 0; x < blocks; x++)
                    {
                        var b = GetFreeBlock(new TwineBlockHeader() { Disused = false });

                        header.Append(b);

                        var r = ms.ReadExactly(buffer, 0, 0, TimeSpan.MaxValue);
                        
                        //we should be at the end of the block header after GetFreeBlock()
                        
                        _stream.Write(buffer, 0, r);
                    }
                    
                    WriteBlock(header.BlockIndex);

                    _header.ChainCount++;
                    
                    _header.WriteSelf(_stream);
                }
                finally
                {
                    _streamLock.Release();
                }

                var entry = new TwineEntry(this, header);
                
                entry.TryCache(item, ms);
                
                ms.Dispose();
                
                _buckets[bucket]!.Add(entry);

                return true;
            }
            finally
            {
                _bucketLock.Release();
            }
        }

        public void Clear()
        {
            if (Disposed) throw new ObjectDisposedException(nameof(Twine<T>));
            
            _bucketLock.Wait();
            _streamLock.Wait();

            try
            {
                foreach (var bucket in _buckets)
                {
                    if (bucket is null) continue;
                    
                    bucket.Clear();
                }

                foreach (var block in _blocks)
                {
                    block.Disused = true;
                    
                    WriteBlock(block.BlockIndex);
                }

                _header.ChainCount = 0;
                
                _header.WriteSelf(_stream);
            }
            finally
            {
                _bucketLock.Release();
                _streamLock.Release();
            }
        }

        public bool Contains(T item)
        {
            if (Disposed) throw new ObjectDisposedException(nameof(Twine<T>));
            
            _bucketLock.Wait();
            
            try
            {
                var eo = item.Encode();

                var hash = eo.GetLongHash();

                var bucket = (int)(hash % BucketCount);

                if (_buckets[bucket] is null) return false;

                foreach (var cacheEntry in _buckets[bucket]!)
                {
                    if (cacheEntry.Header.Hash == hash && item.Equals(cacheEntry.Get()))
                        return true;
                }

                return false;
            }
            finally
            {
                _bucketLock.Release();
            }
        }

        public void CopyTo(T[] array, int arrayIndex)
        {
            if (Disposed) throw new ObjectDisposedException(nameof(Twine<T>));
            
            if (arrayIndex < 0) throw new ArgumentOutOfRangeException(nameof(arrayIndex));

            if (arrayIndex + Count > array.Length) throw new
                ArgumentException("The number of elements in the source ICollection<T> is greater " +
                                  "than the available space from arrayIndex to the end of the destination array.");
            
            _bucketLock.Wait();

            try
            {
                var x = arrayIndex;
                
                foreach (var t in this)
                {
                    array[x] = t;

                    x++;
                }
            }
            finally
            {
                _bucketLock.Release();
            }
        }

        public bool Remove(T item)
        {
            _bucketLock.Wait();

            try
            {
                var eo = item.Encode();

                var hash = eo.GetLongHash();

                var bucket = (int)(hash % BucketCount);

                if (_buckets[bucket] is null) return false;

                TwineEntry? entry = null;

                foreach (var cacheEntry in _buckets[bucket]!)
                {
                    if (cacheEntry.Header.Hash == hash && item.Equals(cacheEntry.Get()))
                    {
                        entry = cacheEntry;
                        break;
                    }
                }

                if (entry is null) return false;

                _streamLock.Wait();

                try
                {
                    entry.Header.Disused = true;

                    foreach (var b in entry.Header)
                    {
                        _blocks[b].Disused = true;
                        
                        WriteBlock(b);
                    }
                    
                    WriteBlock(entry.Header.BlockIndex);

                    _header.ChainCount--;
                    
                    _header.WriteSelf(_stream);
                }
                finally
                {
                    _streamLock.Release();
                }

                _buckets[bucket]!.Remove(entry);

                return true;
            }
            finally
            {
                _bucketLock.Release();
            }
        }
        
        public IEnumerator<T> GetEnumerator()
        {
            return new TwineEnumerator(this);
        }

        IEnumerator IEnumerable.GetEnumerator()
        {
            return GetEnumerator();
        }
    }
}