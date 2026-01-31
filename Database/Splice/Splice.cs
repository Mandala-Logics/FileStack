using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Threading;
using MandalaLogics.Packing;
using MandalaLogics.Encoding;
using MandalaLogics.Weave;

namespace MandalaLogics.Splice
{
    /// <summary>
    /// A typed list backup by a stream, which is thread-safe.
    /// </summary>
    public sealed partial class Splice<T> : IList<T> where T : class, IEncodable
    {
        private const int BlockSize = 512;
        private const int MaxChainLength = 50;
        private const int MaxCacheSize = 64 * 1024 * 1024;

        private static readonly int BlockCapacity;

        public bool Disposed { get; private set; } = false;
        public int Count => _chains.Count;
        public bool IsReadOnly => false;
        
        public T this[int index]
        {
            get
            {
                EnterUpgradableReadLock();

                try
                {
                    if (_cache.TryGet(index) is var enc && enc != null) return (T)enc;

                    _streamLock.EnterWriteLock();

                    try
                    {
                        return ReadFromChain(index);
                    }
                    finally
                    {
                        _streamLock.ExitWriteLock();
                    }
                }
                catch (InvalidCastException)
                {
                    throw new ArgumentException(
                        $"The read type was not '{typeof(T)}' as indicated in the generic argument.");   
                }
                finally
                {
                    _enumLock.ExitWriteLock();
                    _streamLock.ExitUpgradeableReadLock();
                }
            }
            set
            {
                EnterWriteLock();
                
                try
                {
                    WriteToChain(index, value);
                }
                finally
                {
                    _enumLock.ExitWriteLock();
                    _streamLock.ExitWriteLock();
                }
            }
        }
        
        private readonly Stream _stream;
        private readonly SpliceHeader _header;
        private readonly List<BlockHeader> _blocks;
        private readonly List<ChainHeader> _chains;
        private readonly SpliceCache _cache = new SpliceCache(MaxCacheSize);
        private readonly ReaderWriterLockSlim _streamLock = new ReaderWriterLockSlim();
        private readonly ReaderWriterLockSlim _enumLock = new ReaderWriterLockSlim();
        
        static Splice()
        {
            EncodedObject.RegisterAll(Assembly.GetAssembly(typeof(Splice<>)));

            BlockCapacity = BlockSize - BlockHeader.EncodedSize;
        }

        public Splice(Stream stream)
        {
            stream.Seek(0L, SeekOrigin.Begin);

            _stream = stream;

            try
            {
                EncodedValue.Read(stream, out var ev);

                if (ev.Value is SpliceHeader sh)
                {
                    _header = sh;
                }
                else
                {
                    throw new SpliceNotValidException("Failed to decode header.");
                }

                if (!_header.CompareType(typeof(T))) 
                    throw new ArgumentException
                        ("The supplied generic type of this class is probably not the same one with which this splice was initially created.");
            }
            catch (Exception e) when (e is EncodingException)
            {
                throw new SpliceNotValidException("Splice not valid, could not read header.", e);
            }
            catch (EndOfStreamException) //stream is empty, probably
            {
                if (_stream.Length != 0) throw new SpliceNotValidException("Failed to decode header.");

                _header = new SpliceHeader(typeof(T));
                
                stream.SetLength(BlockSize);
            }

            _blocks = new List<BlockHeader>(_header.BlockCount);

            ReadAllBlocks();

            _chains = _blocks.Where(b => b is ChainHeader).Cast<ChainHeader>().ToList();
            
            _chains.Sort((c1, c2) => c1.Ordinal - c2.Ordinal);
        }
        
        private void EnterWriteLock()
        {
            if (_enumLock.TryEnterWriteLock(Weave.Weave.WaitTime))
            {
                _streamLock.EnterWriteLock();
            }
            else
            {
                throw new InvalidOperationException("Splice cannot be modified, or read from the underlying stream, while it's being enumerated.");
            }
        }
        
        private void EnterUpgradableReadLock()
        {
            if (_enumLock.TryEnterWriteLock(Weave.Weave.WaitTime))
            {
                _streamLock.EnterUpgradeableReadLock();
            }
            else
            {
                throw new InvalidOperationException("Splice cannot be modified, or read from the underlying stream, while it's being enumerated.");
            }
        }

        public bool TryFlushOne(Predicate<T> predicate)
        {
            if (Disposed) throw new ObjectDisposedException(nameof(Splice<T>));
            
            _streamLock.EnterReadLock();

            try
            {
                if (_cache.IndexOf(predicate) is { } i && _cache.Get(i) is { } ie)
                {
                    WriteToChain(i, (T)ie);
                    return true;
                }
                else
                {
                    return false;
                }
            }
            finally
            {
                _streamLock.ExitReadLock();
            }
        }

        public bool TryFlush(T item)
        {
            if (Disposed) throw new ObjectDisposedException(nameof(Splice<T>));
            
            EnterWriteLock();

            try
            {
                return _cache.OnFirst<T>(item.Equals, WriteToChain);
            }
            finally
            {
                _enumLock.ExitWriteLock();
                _streamLock.ExitUpgradeableReadLock();
            }
        }

        public int TryFlush(Predicate<T> predicate)
        {
            if (Disposed) throw new ObjectDisposedException(nameof(Splice<T>));
            
            EnterWriteLock();

            try
            {
                int n = 0;
                
                _cache.Foreach<T>((index, item) =>
                {
                    if (predicate.Invoke(item))
                    {
                        n++;
                        
                        WriteToChain(index, item);
                    }
                });

                return n;
            }
            finally
            {
                _enumLock.ExitWriteLock();
                _streamLock.ExitUpgradeableReadLock();
            }
        }

        public bool TryFlush(int index)
        {
            if (Disposed) throw new ObjectDisposedException(nameof(Splice<T>));
            
            EnterWriteLock();

            try
            {
                if (_cache.TryGet(index) is { } ie)
                {
                    WriteToChain(index, (T)ie);
                    return true;
                }
                else
                {
                    return false;
                }
            }
            finally
            {
                _enumLock.ExitWriteLock();
                _streamLock.ExitUpgradeableReadLock();
            }
        }

        public SpliceHandle GetHandle(int index)
        {
            if (Disposed) throw new ObjectDisposedException(nameof(Splice<T>));
            
            return new SpliceHandle(this, index);
        }

        public SpliceHandle GetHandle(Predicate<T> predicate)
        {
            if (Disposed) throw new ObjectDisposedException(nameof(Splice<T>));
            
            EnterWriteLock();

            try
            {
                for (var x = 0; x < _chains.Count; x++)
                {
                    if (_cache.TryGet(x) is { } ie)
                    {
                        if (predicate.Invoke((T)ie)) return new SpliceHandle(this, x);
                    }
                    else
                    {
                        var t = ReadFromChain(x);

                        if (predicate.Invoke(t)) return new SpliceHandle(this, x);
                    }
                }

                throw new InvalidOperationException("No items match the predicate provided.");
            }
            finally
            {
                _enumLock.ExitWriteLock();
                _streamLock.ExitWriteLock();
            }
        }
        
        public SpliceHandle GetHandle(T val)
        {
            if (Disposed) throw new ObjectDisposedException(nameof(Splice<T>));
            
            EnterUpgradableReadLock();

            try
            {
                if (_cache.IndexOf(val) is { } i) return new SpliceHandle(this, i);
                
                _streamLock.EnterWriteLock();

                try
                {
                    for (var x = 0; x < _chains.Count; x++)
                    {
                        if (!_cache.Contains(x))
                        {
                            var t = ReadFromChain(x);

                            if (t.Equals(val)) return new SpliceHandle(this, x);
                        }
                    }

                    throw new InvalidOperationException("No items in the list match the value provided.");
                }
                finally
                {
                    _streamLock.ExitWriteLock();
                }
            }
            finally
            {
                _enumLock.ExitWriteLock();
                _streamLock.ExitUpgradeableReadLock();
            }
        }
        
        public void Add(T item)
        {
            if (Disposed) throw new ObjectDisposedException(nameof(Splice<T>));
            
            EnterWriteLock();

            try
            {
                var chain = CreateChain();
                
                WriteToChain(chain, item);
            }
            finally
            {
                _enumLock.ExitWriteLock();
                _streamLock.ExitWriteLock();
            }
        }

        public void Clear()
        {
            if (Disposed) throw new ObjectDisposedException(nameof(Splice<T>));
            
            EnterWriteLock();

            try
            {
                _cache.Clear();

                for (var x = 0; x < _blocks.Count; x++)
                {
                    _blocks[x] = new BlockHeader { Disused = true };
                    WriteBlock(x);
                }
                
                _chains.Clear();
            }
            finally
            {
                _enumLock.ExitWriteLock();
                _streamLock.ExitWriteLock();
            }
        }

        public bool Contains(T item)
        {
            if (Disposed) throw new ObjectDisposedException(nameof(Splice<T>));
            
            EnterUpgradableReadLock();

            try
            {
                if (_cache.Contains(item)) return true;
                
                _streamLock.EnterWriteLock();

                try
                {
                    for (var x = 0; x < _chains.Count; x++)
                    {
                        if (!_cache.Contains(x))
                        {
                            var t = ReadFromChain(x);

                            if (t.Equals(item)) return true;
                        }
                    }

                    return false;
                }
                finally
                {
                    _streamLock.ExitWriteLock();
                }
            }
            finally
            {
                _enumLock.ExitWriteLock();
                _streamLock.ExitUpgradeableReadLock();
            }
        }

        public void CopyTo(T[] array, int arrayIndex)
        {
            if (Disposed) throw new ObjectDisposedException(nameof(Splice<T>));
            
            if (arrayIndex < 0) throw new ArgumentOutOfRangeException(nameof(arrayIndex));

            if (arrayIndex + Count > array.Length) throw new
                ArgumentException("The number of elements in the source ICollection<T> is greater " +
                                  "than the available space from arrayIndex to the end of the destination array.");
            
            EnterUpgradableReadLock();

            try
            {
                for (var x = 0; x < _chains.Count; x++)
                {
                    if (_cache.TryGet(x) is { } val)
                    {
                        array[arrayIndex] = (T)val;
                    }
                    else
                    {
                        _streamLock.EnterWriteLock();
                        
                        try
                        {
                            array[arrayIndex] = ReadFromChain(x);
                        }
                        finally
                        {
                            _streamLock.ExitWriteLock();
                        }
                    }

                    arrayIndex++;
                }
            }
            finally
            {
                _enumLock.ExitWriteLock();
                _streamLock.ExitUpgradeableReadLock();
            }
        }

        public bool Remove(T item)
        {
            if (Disposed) throw new ObjectDisposedException(nameof(Splice<T>));
            
            EnterWriteLock();

            try
            {
                if (_cache.IndexOf(item) is { } i)
                {
                    _cache.Remove(i);
                    DestroyChain(i);
                    return true;
                }
                
                for (var x = 0; x < _chains.Count; x++)
                {
                    if (!_cache.Contains(x))
                    {
                        var t = ReadFromChain(x);

                        if (t.Equals(item))
                        {
                            _cache.Remove(x);
                            DestroyChain(x);
                            return true;
                        }
                    }
                }

                return false;
            }
            finally
            {
                _enumLock.ExitWriteLock();
                _streamLock.EnterWriteLock();
            }
        }
        
        public int IndexOf(T item)
        {
            if (Disposed) throw new ObjectDisposedException(nameof(Splice<T>));
            
            EnterUpgradableReadLock();

            try
            {
                if (_cache.IndexOf(item) is { } i) return i;
                
                _streamLock.EnterWriteLock();

                try
                {
                    for (var x = 0; x < _chains.Count; x++)
                    {
                        if (!_cache.Contains(x))
                        {
                            var t = ReadFromChain(x);

                            if (t.Equals(item)) return x;
                        }
                    }

                    return -1;
                }
                finally
                {
                    _streamLock.ExitWriteLock();
                }
            }
            finally
            {
                _enumLock.ExitWriteLock();
                _streamLock.ExitUpgradeableReadLock();
            }
        }

        public void Insert(int index, T item)
        {
            if (Disposed) throw new ObjectDisposedException(nameof(Splice<T>));
            
            EnterWriteLock();

            try
            {
                var chainIndex = CreateChain();
                
                WriteToChain(chainIndex, item);

                var chain = _chains[chainIndex];

                chain.Ordinal = index;

                for (var x = chainIndex + 1; x < _chains.Count; x++)
                {
                    chain.Ordinal++;
                }
                
                WriteAllChains();
                
                _chains.Sort((c1, c2) => c1.Ordinal - c2.Ordinal);
            }
            finally
            {
                _enumLock.ExitWriteLock();
                _streamLock.ExitUpgradeableReadLock();
            }
        }

        public void RemoveAt(int index)
        {
            if (Disposed) throw new ObjectDisposedException(nameof(Splice<T>));
            
            EnterWriteLock();

            try
            {
                DestroyChain(index);
            }
            finally
            {
                _enumLock.ExitWriteLock();
                _streamLock.ExitUpgradeableReadLock();
            }
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

                    if (ev.Value is BlockHeader bh)
                    {
                        bh.SetIndex(n - 1);
                        _blocks.Add(bh);
                    }
                    else
                    {
                        throw new SpliceNotValidException("Failed to decode blocks, unexpected value read.");
                    }
                    
                    n++;

                } while (true);
            }
            catch (EndOfStreamException) {}
            catch (Exception e) when (e is EncodingException)
            {
                throw new SpliceNotValidException("Failed to decode blocks", e);
            }
        }

        private T ReadFromChain(int chainIndex)
        {
            var chain = _chains[chainIndex];

            if (_cache.TryGet(chain.BlockIndex) is T t)
            {
                return t;
            }
            
            var seam = new Seam();

            foreach (var blockIndex in chain)
            {
                seam = seam.Append(GetStitch(blockIndex));
            }

            var ms = new MemoryStream(seam.Read(_stream));

            EncodedValue.Read(ms, out var ev);

            if (ev.Value is T ret)
            {
                _cache.TrySet(chain.Ordinal, ret, seam.ByteLength);
                    
                return ret;
            }
            else
            {
                throw new ArgumentException(
                    $"The read type was not '{typeof(T)}' as indicated in the generic argument, but was '{ev.Value.GetType()}'.");
            }
        }

        private void WriteToChain(int chainIndex, T value)
        {
            var ms = value.Encode().WriteToMemoryStream();

            var len = (int)ms.Length;

            var requiredBlocks = Math.DivRem(len, BlockCapacity, out var rem) + (rem == 0 ? 0 : 1);

            if (requiredBlocks > MaxChainLength)
                throw new InvalidOperationException("Cannot store this value because it is too long.");

            var chain = _chains[chainIndex];

            if (chain.Count > requiredBlocks)
            {
                do
                {
                    ShrinkChain(chainIndex);
                        
                } while (chain.Count > requiredBlocks);
            }
            else
            {
                while (chain.Count < requiredBlocks)
                {
                    ExtendChain(chainIndex);
                }
            }

            var seam = new Seam();

            foreach (var blockIndex in chain)
            {
                seam = seam.Append(GetStitch(blockIndex));
            }

            var w = seam.Write(ms.GetBuffer(), 0, len, _stream);

            if (w == 0) throw new ProgrammerException("nothing written");
            
            if (ms.GetBuffer()[0] == 0) throw new ProgrammerException("empty buffer");
                
            _cache.TrySet(chain.Ordinal, value, len);
            
            ms.Dispose();
        }

        private void WriteAllChains()
        {
            foreach (var chain in _chains)
            {
                WriteBlock(chain.BlockIndex);
            }
        }

        private void SetChainOrdinals()
        {
            for (var x = 0; x < _chains.Count; x++)
            {
                _chains[x].Ordinal = x;
            }
        }

        private void DestroyChain(int chainIndex)
        {
            var chain = _chains[chainIndex];
                
            foreach (var blockIndex in chain)
            {
                _blocks[blockIndex] = new BlockHeader { Disused = true };
                
                WriteBlock(blockIndex);
            }
                
            _chains.RemoveAt(chainIndex);
                
            SetChainOrdinals();

            _blocks[chain.BlockIndex] = new BlockHeader { Disused = true };
                
            WriteBlock(chain.BlockIndex);
                
            WriteAllChains();
            
            _chains.Sort((c1, c2) => c1.Ordinal - c2.Ordinal);
        }

        private void ShrinkChain(int chainIndex)
        {
            var blockIndex = _chains[chainIndex][^1];

            _blocks[blockIndex] = new BlockHeader { Disused = true };
                
            WriteBlock(blockIndex);
                
            _chains[chainIndex].Truncate(1);
                
            WriteBlock(_chains[chainIndex].BlockIndex);
        }

        private void ExtendChain(int chainIndex)
        {
            var block = GetFreeBlock(new BlockHeader { Disused = false });

            _chains[chainIndex].Append(block);
                
            WriteBlock(_chains[chainIndex].BlockIndex);
        }

        private int CreateChain()
        {
            var block = GetFreeBlock(new BlockHeader { Disused = false });

            var chain = new ChainHeader { Disused = false, Ordinal = _chains.Count };
            chain.Append(block);

            GetFreeBlock(chain);
                
            _chains.Add(chain);
                
            WriteBlock(chain.BlockIndex);

            return _chains.Count - 1;
        }

        private int GetFreeBlock(BlockHeader header)
        {
            for (var x = 0; x < _blocks.Count; x++)
            {
                if (_blocks[x].Disused)
                {
                    _blocks[x] = header;
                    WriteBlock(x);
                    return x;
                }
            }

            return CreateBlock(header);
        }

        private int CreateBlock(BlockHeader block)
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

        private Stitch GetStitch(int index) => new Stitch((index + 1) * BlockSize + BlockHeader.EncodedSize, BlockSize - BlockHeader.EncodedSize);
        
        public void Dispose()
        {
            if (Disposed) return;
            
            EnterWriteLock();

            Disposed = true;
            
            _stream.Flush();
            _stream.Dispose();
            
            _enumLock.ExitWriteLock();
            _streamLock.ExitWriteLock();
        }

        public IEnumerator<T> GetEnumerator()
        {
            return new SpliceEnumerator(this);
        }

        IEnumerator IEnumerable.GetEnumerator()
        {
            return GetEnumerator();
        }
    }
}