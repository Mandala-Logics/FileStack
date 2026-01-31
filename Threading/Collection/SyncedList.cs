using System;
using System.Collections;
using System.Collections.Generic;
using System.Threading;

namespace MandalaLogics.Collection
{
    public sealed partial class SyncedList<T> : IList<T>
    {
        private static readonly TimeSpan WaitTime = TimeSpan.FromMilliseconds(500);

        public int Count
        {
            get
            {
                _lockSlim.EnterReadLock();

                var ret = _baseList.Count;
                
                _lockSlim.ExitReadLock();

                return ret;
            }
        }

        public T this[int index]
        {
            get
            {
                _lockSlim.EnterReadLock();

                var ret = _baseList[index];
                
                _lockSlim.ExitReadLock();

                return ret;
            }

            set
            {
                if (!WaitForWriteLock(WaitTime)) { throw new InvalidOperationException("Collection cannot be modified while it is being enumerated."); }

                _baseList[index] = value;

                _lockSlim.ExitWriteLock();
            }
        }

        public bool IsReadOnly { get; } = false;

        private readonly List<T> _baseList;
        private readonly ReaderWriterLockSlim _lockSlim = new ReaderWriterLockSlim(LockRecursionPolicy.SupportsRecursion);

        public SyncedList()
        {
            _baseList = new List<T>();
        }
        
        public SyncedList(int capacity)
        {
            _baseList = new List<T>(capacity);
        }

        public bool WaitForWriteLock(TimeSpan timeout)
        {
            if (_lockSlim.IsWriteLockHeld) { return true; }
            else
            {
                if (!_lockSlim.TryEnterWriteLock(timeout))
                {
                    return false;
                }
                else { return true; }
            }
        }

        public void ExitWriteLock()
        {
            if (!_lockSlim.IsWriteLockHeld) { throw new InvalidOperationException("A write lock is not held by this thread."); }

            _lockSlim.ExitWriteLock();
        }

        public void Add(T item)
        {
            if (!WaitForWriteLock(WaitTime)) { throw new InvalidOperationException("Collection cannot be modified while it is being enumerated."); }

            try
            {
                _baseList.Add(item);
            }
            finally
            {
                _lockSlim.ExitWriteLock();
            }
        }

        public void Clear()
        {
            if (!WaitForWriteLock(WaitTime)) { throw new InvalidOperationException("Collection cannot be modified while it is being enumerated."); }

            try
            {
                _baseList.Clear();
            }
            finally
            {
                _lockSlim.ExitWriteLock();
            }
        }

        public bool Contains(T item)
        {
            _lockSlim.EnterReadLock();

            try
            {
                return _baseList.Contains(item);
            }
            finally
            {
                _lockSlim.ExitReadLock();
            }
        }

        public void CopyTo(T[] array, int arrayIndex)
        {
            _lockSlim.EnterReadLock();

            try
            {
                _baseList.CopyTo(array, arrayIndex);
            }
            finally
            {
                _lockSlim.ExitReadLock();
            }
        }

        public IEnumerator<T> GetEnumerator() => new SyncedListEnumerator(this);
        IEnumerator IEnumerable.GetEnumerator() => new SyncedListEnumerator(this);

        public int IndexOf(T item)
        {
            _lockSlim.EnterReadLock();

            try
            {
                return _baseList.IndexOf(item);
            }
            finally
            {
                _lockSlim.ExitReadLock();
            }
        }

        public void Sort(Comparison<T> comparison)
        {
            if (!WaitForWriteLock(WaitTime)) { throw new InvalidOperationException("Collection cannot be modified while it is being enumerated."); }

            try
            {
                _baseList.Sort(comparison);
            }
            finally
            {
                _lockSlim.ExitWriteLock();                
            }
        }

        public void Insert(int index, T item)
        {
            if (!WaitForWriteLock(WaitTime)) { throw new InvalidOperationException("Collection cannot be modified while it is being enumerated."); }

            try
            {
                _baseList.Insert(index, item);
            }
            finally
            {
                _lockSlim.ExitWriteLock();
            }
        }

        public bool Remove(T item)
        {
            if (!WaitForWriteLock(WaitTime)) { throw new InvalidOperationException("Collection cannot be modified while it is being enumerated."); }

            try
            {
                return _baseList.Remove(item);
            }
            finally
            {
                _lockSlim.ExitWriteLock();
            }
        }

        public void RemoveAt(int index)
        {
            if (!WaitForWriteLock(WaitTime)) { throw new InvalidOperationException("Collection cannot be modified while it is being enumerated."); }

            try
            {
                _baseList.RemoveAt(index);
            }
            finally
            {
                _lockSlim.ExitWriteLock();
            }
        }
    }
}