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
    public sealed partial class Cord<TKey, TValue> : IDictionary<TKey, TValue> 
        where TKey : class, IEncodable where TValue : class, IEncodable
    {
        private const int BlockSize = 512;
        private const int MaxChainLength = 46;
        private const int MaxCacheSize = 128 * 1024;
        private const ulong BucketCount = 256;
        
        private static readonly int BlockCapacity;
        
        public bool Disposed { get; private set; } = false;

        public int Count => _header.ChainCount;
        public bool IsReadOnly => false;
        
        public TValue this[TKey key]
        {
            get => TryGetValue(key, out var value) ? value : throw new KeyNotFoundException();
            set => DoSet(key, value);
        }

        private readonly Stream _stream;
        private readonly CordHeader _header;
        private readonly List<CordBlockHeader> _blocks;

        private readonly List<CordEntry>?[] _buckets;

        public ICollection<TKey> Keys => new CordKeyCollection(this);
        public ICollection<TValue> Values => new CordValueCollection(this);
        
        private readonly SemaphoreSlim _streamLock = new SemaphoreSlim(1);
        private readonly SemaphoreSlim _cacheLock = new SemaphoreSlim(1);
        private readonly SemaphoreSlim _bucketLock = new SemaphoreSlim(1);
        
        private readonly int _maxCacheSize;
        private int _cacheSize = 0;
        
        static Cord()
        {
            EncodingRegister.RegisterAll(Assembly.GetAssembly(typeof(CordChainHeader)));

            BlockCapacity = BlockSize - CordBlockHeader.EncodedSize;
        }
        
        public Cord(Stream stream, int maxCacheSize = MaxCacheSize)
        {
            if (maxCacheSize < 0) _maxCacheSize = 0;
            else if (maxCacheSize > MaxCacheSize) _maxCacheSize = MaxCacheSize;
            else _maxCacheSize = maxCacheSize;
            
            stream.Seek(0L, SeekOrigin.Begin);

            _stream = stream;
            
            try
            {
                EncodedValue.Read(stream, out var ev);

                if (ev.Value is CordHeader ch)
                {
                    _header = ch;
                }
                else
                {
                    throw new CordNotValidException("Failed to decode header.");
                }

                _header.CompareKeyType(typeof(TKey));
                _header.CompareValueType(typeof(TValue));
            }
            catch (Exception e) when (e is EncodingException)
            {
                throw new CordNotValidException("Splice not valid, could not read header.", e);
            }
            catch (EndOfStreamException) //stream is empty, probably
            {
                if (_stream.Length != 0) throw new CordNotValidException("Failed to decode header.");

                _header = new CordHeader(typeof(TKey), typeof(TValue));
                _header.WriteSelf(_stream);
                
                stream.SetLength(BlockSize);
            }

            _blocks = new List<CordBlockHeader>(_header.BlockCount);

            _buckets = new List<CordEntry>[BucketCount];
            
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

                    if (ev.Value is CordChainHeader ch)
                    {
                        ch.SetIndex(n - 1);
                        _blocks.Add(ch);

                        var bucket = (int)(ch.KeyHash % BucketCount);

                        _buckets[bucket] ??= new List<CordEntry>();
                        
                        _buckets[bucket]!.Add(new CordEntry(this, ch));
                    }
                    else if (ev.Value is CordBlockHeader bh)
                    {
                        bh.SetIndex(n - 1);
                        _blocks.Add(bh);
                    }
                    else
                    {
                        throw new CordNotValidException("Failed to decode blocks, unexpected value read.");
                    }
                    
                    n++;

                } while (true);
            }
            catch (EndOfStreamException) {}
            catch (Exception e) when (e is EncodingException)
            {
                throw new CordNotValidException("Failed to decode blocks", e);
            }
        }

        public bool TryFlush(TKey key)
        {
            if (Disposed) throw new ObjectDisposedException(nameof(Cord<TKey, TValue>));
            
            DoSet(key, this[key]);

            return true;
        }

        public CordHandle GetHandle(TKey key)
        {
            if (Disposed) throw new ObjectDisposedException(nameof(Cord<TKey, TValue>));

            return new CordHandle(this, key, this[key]);
        }
        
        private int GetFreeBlock(CordBlockHeader header)
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

        private int CreateBlock(CordBlockHeader block)
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
        
        private Stitch GetStitch(int index) => new Stitch((index + 1) * BlockSize + CordBlockHeader.EncodedSize, BlockSize - CordBlockHeader.EncodedSize);

        internal void DoSet(TKey key, TValue value)
        {
            _bucketLock.Wait();

            try
            {
                var encodedKey = key.Encode();

                var keyHash = encodedKey.GetLongHash();

                var bucket = (int)(keyHash % BucketCount);

                if (_buckets[bucket] is null)
                {
                    throw new KeyNotFoundException();
                }

                CordEntry? entry = null;

                foreach (var ce in _buckets[bucket]!)
                {
                    if (ce.Header.KeyHash == keyHash && ce.GetKey().Equals(key))
                    {
                        entry = ce;
                        break;
                    }
                }

                if (entry is null) throw new KeyNotFoundException();

                var encodedValue = value.Encode();
                var valueHash = encodedValue.GetLongHash();

                var ms = new MemoryStream();

                encodedKey.Write(ms);
                var keyLength = (int)ms.Length;
                encodedValue.Write(ms);

                var blocks = Math.DivRem((int)ms.Length, BlockCapacity, out var rem) + (rem > 0 ? 1 : 0);

                if (blocks > MaxChainLength)
                    throw new ArgumentException("Cannot encoded this value into Twine as it is too large.");

                var header = new CordChainHeader(keyHash, valueHash) { Disused = false };

                _streamLock.Wait();

                try
                {
                    //erase old blocks

                    foreach (var block in entry.Header)
                    {
                        _blocks[block].Disused = true;

                        WriteBlock(block);
                    }

                    entry.Header.Disused = true;

                    WriteBlock(entry.Header.BlockIndex);

                    _buckets[bucket]!.Remove(entry);

                    //create new blocks

                    GetFreeBlock(header);

                    var buffer = new byte[BlockCapacity];
                    ms.Position = 0L;

                    for (var x = 0; x < blocks; x++)
                    {
                        var b = GetFreeBlock(new CordBlockHeader() { Disused = false });

                        header.Append(b);

                        var r = ms.ReadExactly(buffer, 0, buffer.Length, TimeSpan.MaxValue);

                        //we should be at the end of the block header after GetFreeBlock()

                        _stream.Write(buffer, 0, r);
                    }

                    WriteBlock(header.BlockIndex);
                }
                finally
                {
                    _stream.Flush();
                    _streamLock.Release();
                }

                entry = new CordEntry(this, header);

                entry.SetKey(key, keyLength);
                entry.TryCacheValue(value, (int)ms.Length - keyLength);

                _buckets[bucket]!.Add(entry);
            }
            finally
            {
                _bucketLock.Release();
            }
        }
        
        public void Add(KeyValuePair<TKey, TValue> item)
        {
            Add(item.Key, item.Value);
        }

        public void Clear()
        {
            if (Disposed) throw new ObjectDisposedException(nameof(Cord<TKey, TValue>));
            
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

        public bool Contains(KeyValuePair<TKey, TValue> item)
        {
            if (Disposed) throw new ObjectDisposedException(nameof(Cord<TKey, TValue>));
            
            var eo = item.Key.Encode();

            var hash = eo.GetLongHash();

            var bucket = (int)(hash % BucketCount);
            
            _bucketLock.Wait();
            
            try
            {
                if (_buckets[bucket] is null) return false;

                foreach (var entry in _buckets[bucket]!)
                {
                    if (entry.Header.KeyHash == hash 
                        && entry.GetKey().Equals(item.Key)
                        && entry.GetValue().Equals(item.Value)) 
                        return true;
                }

                return false;
            }
            finally
            {
                _bucketLock.Release();
            }
        }

        public void CopyTo(KeyValuePair<TKey, TValue>[] array, int arrayIndex)
        {
            if (Disposed) throw new ObjectDisposedException(nameof(Cord<TKey, TValue>));
            
            if (arrayIndex < 0) throw new ArgumentOutOfRangeException(nameof(arrayIndex));

            if (arrayIndex + Count > array.Length) throw new
                ArgumentException("The number of elements in the source ICollection<T> is greater " +
                                  "than the available space from arrayIndex to the end of the destination array.");

            var x = arrayIndex;
            
            foreach (var kvp in this)
            {
                array[x] = kvp;
                
                x++;
            }
        }

        public bool Remove(KeyValuePair<TKey, TValue> item)
        {
            if (Disposed) throw new ObjectDisposedException(nameof(Cord<TKey, TValue>));
            
            var eo = item.Key.Encode();

            var hash = eo.GetLongHash();

            var bucket = (int)(hash % BucketCount);
            
            _bucketLock.Wait();
            
            try
            {
                if (_buckets[bucket] is null)
                {
                    return false;
                }

                CordEntry? entry = null;

                foreach (var ce in _buckets[bucket]!)
                {
                    if (ce.Header.KeyHash == hash && ce.GetKey().Equals(item.Key)
                        && ce.GetValue().Equals(item.Value))
                    {
                        entry = ce;
                        break;
                    }
                }

                if (entry is null) return false;

                _streamLock.Wait();

                try
                {
                    entry.Header.Disused = true;
                    
                    WriteBlock(entry.Header.BlockIndex);

                    foreach (var block in entry.Header)
                    {
                        _blocks[block].Disused = true;
                        
                        WriteBlock(block);
                    }

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
        
        public void Add(TKey key, TValue value)
        {
            if (Disposed) throw new ObjectDisposedException(nameof(Cord<TKey, TValue>));
            
            _bucketLock.Wait();
            
            try
            {
                var encodedKey = key.Encode();

                var keyHash = encodedKey.GetLongHash();

                var bucket = (int)(keyHash % BucketCount);

                _buckets[bucket] ??= new List<CordEntry>();
                
                foreach (var ce in _buckets[bucket]!)
                {
                    if (ce.Header.KeyHash == keyHash && ce.GetKey().Equals(key))
                        throw new ArgumentException("Key already exists.");
                }

                var encodedValue = value.Encode();

                var valueHash = encodedValue.GetLongHash();

                using var ms = new MemoryStream();

                encodedKey.Write(ms);
                var keyLength = (int)ms.Length;
                
                encodedValue.Write(ms);
                
                var blocks = Math.DivRem((int)ms.Length, BlockCapacity, out var rem) + (rem > 0 ? 1 : 0);
                
                if (blocks > MaxChainLength)
                    throw new ArgumentException("Cannot encoded this value into Cord as it is too large.");

                var header = new CordChainHeader(keyHash, valueHash) { Disused = false };
                
                _streamLock.Wait();
                
                try
                {
                    GetFreeBlock(header);
                    
                    var buffer = new byte[BlockCapacity];
                    ms.Position = 0L;
                    
                    for (var x = 0; x < blocks; x++)
                    {
                        var b = GetFreeBlock(new CordBlockHeader() { Disused = false });

                        header.Append(b);

                        var r = ms.ReadExactly(buffer, 0, buffer.Length, TimeSpan.MaxValue);
                        
                        //we should be at the end of the block header after GetFreeBlock()
                        
                        _stream.Write(buffer, 0, r);
                    }
                    
                    WriteBlock(header.BlockIndex);

                    _header.ChainCount += 1;
                    
                    _header.WriteSelf(_stream);
                }
                finally
                {
                    _stream.Flush();
                    _streamLock.Release();
                }

                var entry = new CordEntry(this, header);
                
                entry.SetKey(key, keyLength);
                entry.TryCacheValue(value, (int)ms.Length - keyLength);
                
                _buckets[bucket]!.Add(entry);
            }
            finally
            {
                _bucketLock.Release();
            }
        }

        public bool ContainsKey(TKey key)
        {
            if (Disposed) throw new ObjectDisposedException(nameof(Cord<TKey, TValue>));
            
            _bucketLock.Wait();
            
            try
            {
                var eo = key.Encode();

                var hash = eo.GetLongHash();

                var bucket = (int)(hash % BucketCount);

                if (_buckets[bucket] is null) return false;

                foreach (var entry in _buckets[bucket]!)
                {
                    if (entry.Header.KeyHash == hash && entry.GetKey().Equals(key))
                        return true;
                }

                return false;
            }
            finally
            {
                _bucketLock.Release();
            }
        }

        public bool Remove(TKey key)
        {
            _bucketLock.Wait();
            
            try
            {
                var eo = key.Encode();

                var hash = eo.GetLongHash();

                var bucket = (int)(hash % BucketCount);

                if (_buckets[bucket] is null)
                {
                    return false;
                }

                CordEntry? entry = null;

                foreach (var ce in _buckets[bucket]!)
                {
                    if (ce.Header.KeyHash == hash && ce.GetKey().Equals(key))
                    {
                        entry = ce;
                        break;
                    }
                }

                if (entry is null) return false;

                _streamLock.Wait();

                try
                {
                    entry.Header.Disused = true;
                    
                    WriteBlock(entry.Header.BlockIndex);

                    foreach (var block in entry.Header)
                    {
                        _blocks[block].Disused = true;
                        
                        WriteBlock(block);
                    }

                    _header.ChainCount--;
                    
                    _header.WriteSelf(_stream);
                }
                finally
                {
                    _stream.Flush();
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

        public bool TryGetValue(TKey key, out TValue value)
        {
            if (Disposed) throw new ObjectDisposedException(nameof(Cord<TKey, TValue>));
            
            _bucketLock.Wait();
            
            try
            {
                var eo = key.Encode();

                var hash = eo.GetLongHash();

                var bucket = (int)(hash % BucketCount);

                if (_buckets[bucket] is null)
                {
                    value = null!;
                    return false;
                }

                foreach (var entry in _buckets[bucket]!)
                {
                    if (entry.Header.KeyHash == hash && entry.GetKey().Equals(key))
                    {
                        value = entry.GetValue();
                        return true;
                    }
                }

                value = null!;
                return false;
            }
            finally
            {
                _bucketLock.Release();
            }
        }
        
        public void Dispose()
        {
            if (Disposed) return;

            _bucketLock.Wait();

            Disposed = true;
            
            _stream.Flush();
            _stream.Dispose();

            _bucketLock.Release();
        }
        
        public IEnumerator<KeyValuePair<TKey, TValue>> GetEnumerator()
        {
            return new CordEnumerator(this);
        }

        IEnumerator IEnumerable.GetEnumerator()
        {
            return GetEnumerator();
        }
    }
}