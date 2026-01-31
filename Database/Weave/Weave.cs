using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using MandalaLogics.Encoding;
using MandalaLogics.Database;

namespace MandalaLogics.Weave
{
    public partial class Weave : IList<IEncodable>, IDisposable
    {
        public const int MaxCacheSize = 67_108_864;
        public static readonly TimeSpan WaitTime = TimeSpan.FromMilliseconds(500);
        
        public IEncodable this[int index]
        {
            get
            {
                if (Disposed) throw new ObjectDisposedException(nameof(Weave));
                
                if (index < 0 || index >= _header.Value.Count) throw new ArgumentOutOfRangeException(nameof(index));
                
                _headerLock.EnterReadLock();

                try
                {
                    if (_cache.TryGet(index) is { } val)
                    {
                        return val;
                    }
                    else
                    {
                        using var strand = _braid.GetStrand(_header.Value[index]);

                        var r = EncodedValue.Read(strand, out EncodedValue ev);

                        var ret = (IEncodable)ev.Value;

                        _cache.TrySet(index, ret, r);

                        return ret;
                    }
                }
                finally
                {
                    _headerLock.ExitReadLock();
                }
            }
            set
            {
                if (Disposed) throw new ObjectDisposedException(nameof(Weave));
                
                EnterWriteLock();

                try
                {
                    using var strand = _braid.GetStrand(_header.Value[index]);

                    var r = value.Encode().Write(strand);
                    
                    strand.SetLength(r);

                    _cache.TrySet(index, value, r);
                }
                finally
                {
                    _headerLock.EnterWriteLock();
                    _enumLock.ExitWriteLock();
                }
            }
        }
        
        public int Count => _header.Value.Count;
        public bool IsReadOnly => false;
        public bool Disposed => _braid.Disposed;
        
        private readonly Packing.Braid _braid;
        private readonly HeaderFile<WeaveHeader> _header;
        private readonly ReaderWriterLockSlim _headerLock = new ReaderWriterLockSlim();
        private readonly ReaderWriterLockSlim _enumLock = new ReaderWriterLockSlim();
        private readonly WeaveCache _cache;

        public Weave(Stream stream, int maxCacheSize = 4096)
        {
            if (maxCacheSize < 1024 || maxCacheSize > MaxCacheSize)
                throw new ArgumentOutOfRangeException(nameof(maxCacheSize));
            
            _braid = new Packing.Braid(stream);

            if (_braid.Count == 0) //new weave
            {
                var strand = _braid.CreateStrand();

                _header = new HeaderFile<WeaveHeader>(strand);
                
                _header.SetValue(new WeaveHeader());
            }
            else //reopen
            {
                var strand = _braid.GetStrand(1U);

                try
                {
                    _header = new HeaderFile<WeaveHeader>(strand);
                    _header.Flush();
                }
                catch (HeaderFileNotValidException)
                {
                    throw new WeaveNotValidException();
                }
            }
            
            _cache = new WeaveCache(maxCacheSize);
        }

        private void EnterWriteLock()
        {
            if (_enumLock.TryEnterWriteLock(WaitTime))
            {
                _headerLock.EnterWriteLock();
            }
            else
            {
                throw new InvalidOperationException("Weave cannot be modified while it's being enumerated.");
            }
        }
        
        private void EnterUpgradableReadLock()
        {
            if (_enumLock.TryEnterWriteLock(WaitTime))
            {
                _headerLock.EnterUpgradeableReadLock();
            }
            else
            {
                throw new InvalidOperationException("Weave cannot be modified while it's being enumerated.");
            }
        }

        public WeaveHandle<T> GetHandle<T>(Predicate<T> predicate) where T : IEncodable
        {
            if (Disposed) throw new ObjectDisposedException(nameof(Weave));
            
            _headerLock.EnterReadLock();

            try
            {
                for (var x = 0; x < _header.Value.Count; x++)
                {
                    if (_cache.Contains(x))
                    {
                        if (_cache.Get(x) is T item && predicate.Invoke(item))
                        {
                            return new WeaveHandle<T>(this, _header.Value[x], item);
                        }
                    }
                    else
                    {
                        using var strand = _braid.GetStrand(_header.Value[x]);

                        var r = EncodedValue.Read(strand, out var ev);

                        var val = (IEncodable)ev.Value;

                        _cache.TrySet(x, val, r);

                        if (val is T item && predicate.Invoke(item))
                        {
                            return new WeaveHandle<T>(this, _header.Value[x], item);
                        }
                    }
                }

                throw new InvalidOperationException("No item in the list matches the predicate.");
            }
            finally
            {
                _headerLock.ExitReadLock();
            }
        }

        public WeaveHandle<T> GetHandle<T>(int index) where T : IEncodable
        {
            if (Disposed) throw new ObjectDisposedException(nameof(Weave));
            
            if (index < 0 || index >= _header.Value.Count) throw new ArgumentOutOfRangeException(nameof(index));
            
            _headerLock.EnterReadLock();

            try
            {
                var item = this[index];

                if (item is T value)
                {
                    return new WeaveHandle<T>(this, _header.Value[index], value);
                }
                else
                {
                    throw new InvalidOperationException($"The item ({item.GetType()}) cannot be cast to the desired type - {typeof(T)}.");
                }
            }
            finally
            {
                _headerLock.ExitReadLock();
            }
        }

        public void RemoveWhere(Predicate<IEncodable> predicate)
        {
            if (Disposed) throw new ObjectDisposedException(nameof(Weave));
            
            EnterUpgradableReadLock();
            
            try
            {
                for (var x = 0; x < _header.Value.Count; x++)
                {
                    if (_cache.Contains(x)) //found in the cache
                    {
                        if (predicate.Invoke(_cache.Get(x)))
                        {
                            _headerLock.EnterWriteLock();

                            try
                            {
                                _braid.DestroyStrand(_header.Value[x]);
                                _header.Value.RemoveAt(x);
                                _header.Flush();

                                _cache.Remove(x);
                            }
                            finally
                            {
                                _headerLock.EnterWriteLock();
                            }
                        }
                    }
                    else
                    {
                        IEncodable val;
                        
                        using (var strand = _braid.GetStrand(_header.Value[x]))
                        {
                            EncodedValue.Read(strand, out var ev);

                            val = (IEncodable)ev.Value;
                        }

                        if (predicate.Invoke(val)) //found one we want to remove
                        {
                            _headerLock.EnterWriteLock();

                            try
                            {
                                _braid.DestroyStrand(_header.Value[x]);
                                _header.Value.RemoveAt(x);
                                _header.Flush();
                                
                                _cache.Remove(x);
                            }
                            finally
                            {
                                _headerLock.EnterWriteLock();
                            }
                        }
                    }
                }
            }
            finally
            {
                _headerLock.ExitUpgradeableReadLock();
                _enumLock.ExitWriteLock();
            }
        }
        
        public void Add(IEncodable item)
        {
            if (Disposed) throw new ObjectDisposedException(nameof(Weave));
            
            EnterWriteLock();
            
            try
            {
                using var strand = _braid.CreateStrand(item);
                
                _header.Value.Add(strand.Id);
                
                _header.Flush();

                _cache.TrySet(_header.Value.Count - 1, item, (int)strand.Length);
            }
            finally
            {
                _headerLock.ExitWriteLock();
                _enumLock.ExitWriteLock();
            }
        }

        public void Clear()
        {
            if (Disposed) throw new ObjectDisposedException(nameof(Weave));
            
            EnterWriteLock();
            
            try
            {
                _braid.Clear();
            
                _header.Value.Clear();
            
                _header.Flush();
            }
            finally
            {
                _headerLock.ExitWriteLock();
                _enumLock.ExitWriteLock();
            }
        }

        public bool Contains(IEncodable item)
        {
            if (Disposed) throw new ObjectDisposedException(nameof(Weave));
            
            _headerLock.EnterReadLock();

            try
            {
                for (var x = 0; x < _header.Value.Count; x++)
                {
                    if (_cache.Contains(x))
                    {
                        if (_cache.Get(x).Equals(item)) return true;
                    }
                    else
                    {
                        using var strand = _braid.GetStrand(_header.Value[x]);

                        var r = EncodedValue.Read(strand, out var ev);

                        var val = (IEncodable)ev.Value;

                        _cache.TrySet(x, val, r);

                        if (val.Equals(item)) return true;
                    }
                }

                return false;
            }
            finally
            {
                _headerLock.ExitReadLock();
            }
        }

        public void CopyTo(IEncodable[] array, int arrayIndex)
        {
            if (Disposed) throw new ObjectDisposedException(nameof(Weave));
            
            if (arrayIndex < 0) throw new ArgumentOutOfRangeException(nameof(arrayIndex));

            if (arrayIndex + Count > array.Length) throw new ArgumentException("The number of elements in the source ICollection<T> is greater than the available space from arrayIndex to the end of the destination array.");
            
            _headerLock.EnterReadLock();

            try
            {
                var en = GetEnumerator();

                try
                {
                    for (var x = arrayIndex; x < Count + arrayIndex; x++)
                    {
                        en.MoveNext();

                        array[x] = en.Current;
                    }
                }
                finally
                {
                    en.Dispose();
                }
            }
            finally
            {
                _headerLock.ExitReadLock();
            }
        }

        public bool Remove(IEncodable item)
        {
            if (Disposed) throw new ObjectDisposedException(nameof(Weave));

            EnterUpgradableReadLock();

            try
            {
                for (var x = 0; x < _header.Value.Count; x++)
                {
                    if (_cache.Contains(x))
                    {
                        if (_cache.Get(x).Equals(item))
                        {
                            _headerLock.EnterWriteLock();

                            try
                            {
                                _braid.DestroyStrand(_header.Value[x]);
                                _header.Value.RemoveAt(x);
                                _header.Flush();

                                _cache.Remove(x);
                                    
                                return true;
                            }
                            finally
                            {
                                _headerLock.EnterWriteLock();
                            }
                        }
                    }
                    else
                    {
                        IEncodable val;
                        
                        using (var strand = _braid.GetStrand(_header.Value[x]))
                        {
                            EncodedValue.Read(strand, out var ev);

                            val = (IEncodable)ev.Value;
                        }

                        if (val.Equals(item)) //found the one we want to remove
                        {
                            _headerLock.EnterWriteLock();

                            try
                            {
                                _braid.DestroyStrand(_header.Value[x]);
                                _header.Value.RemoveAt(x);
                                _header.Flush();
                                
                                _cache.Remove(x);

                                return true;
                            }
                            finally
                            {
                                _headerLock.EnterWriteLock();
                            }
                        }
                    }
                }

                return false;
            }
            finally
            {
                _headerLock.ExitUpgradeableReadLock();
                _enumLock.ExitWriteLock();
            }
        }

        public IEnumerator<IEncodable> GetEnumerator() => new WeaveEnumerator(this);

        IEnumerator IEnumerable.GetEnumerator() => new WeaveEnumerator(this);

        public int IndexOf(IEncodable item)
        {
            if (Disposed) throw new ObjectDisposedException(nameof(Weave));
            
            _headerLock.EnterReadLock();

            try
            {
                for (var x = 0; x < _header.Value.Count; x++)
                {
                    if (_cache.Contains(x))
                    {
                        if (_cache.Get(x).Equals(item)) return x;
                    }
                    else
                    {
                        using var strand = _braid.GetStrand(_header.Value[x]);

                        var r = EncodedValue.Read(strand, out var ev);

                        var val = (IEncodable)ev.Value;

                        _cache.TrySet(x, val, r);

                        if (val.Equals(item)) return x;
                    }
                }

                return -1;
            }
            finally
            {
                _enumLock.ExitWriteLock();
                _headerLock.ExitReadLock();
            }
        }

        public void Insert(int index, IEncodable item)
        {
            if (Disposed) throw new ObjectDisposedException(nameof(Weave));

            if (index < 0 || index >= Count) throw new ArgumentOutOfRangeException(nameof(index));
            
            EnterWriteLock();
            
            try
            {
                using var strand = _braid.CreateStrand(item);
                
                _header.Value.Insert(index, strand.Id);
                
                _header.Flush();

                _cache.Clear();
            }
            finally
            {
                _enumLock.ExitWriteLock();
                _headerLock.ExitWriteLock();
            }
        }

        public void RemoveAt(int index)
        {
            if (Disposed) throw new ObjectDisposedException(nameof(Weave));

            if (index < 0 || index >= Count) throw new ArgumentOutOfRangeException(nameof(index));
            
            EnterWriteLock();
            
            try
            {
                var id = _header.Value[index];
                
                _braid.DestroyStrand(id);
                
                _header.Value.RemoveAt(index);
                
                _header.Flush();
                
                _cache.Remove(index);
            }
            finally
            {
                _enumLock.ExitWriteLock();
                _headerLock.ExitWriteLock();
            }
        }

        public void Dispose()
        {
            _cache.Clear();
            _braid.Dispose();
        }
    }
}