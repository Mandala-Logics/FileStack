using System;
using System.Collections;
using System.Collections.Generic;
using System.Threading;

namespace MandalaLogics.Collection
{
    public sealed partial class SyncedList<T>
    {
        public class SyncedListEnumerator : IEnumerator<T>
        {
            public T Current { get; private set; }
            object? IEnumerator.Current => Current;

            private readonly SyncedList<T> _owner;
            private readonly int _threadId;
            private IEnumerator<T>? _baseEnum;

            public SyncedListEnumerator(SyncedList<T> owner)
            {
                _owner = owner;
                _threadId = Thread.CurrentThread.ManagedThreadId;
                
                _owner._lockSlim.EnterReadLock();

                _baseEnum = owner._baseList.GetEnumerator();
                Current = _baseEnum.Current;
            }
            
            public bool MoveNext()
            {
                CheckThreadId();

                if (_baseEnum is null)
                {
                    throw new ObjectDisposedException("SyncedListEnumerator");
                }

                if (_baseEnum.MoveNext())
                {
                    return true;
                }
                else
                {
                    _baseEnum.Dispose();
                    _baseEnum = null;

                    _owner._lockSlim.ExitReadLock();
                    
                    return false;
                }
            }

            public void Reset()
            {
                CheckThreadId();

                if (_baseEnum is object)
                {
                    _baseEnum.Dispose();
                }
                else
                {
                    _owner._lockSlim.EnterReadLock();
                }

                _baseEnum = _owner._baseList.GetEnumerator();
                Current = _baseEnum.Current;
            }
            
            public void Dispose()
            {
                if (_baseEnum is null)
                {
                    return;
                }
                
                _baseEnum.Dispose();
                _baseEnum = null;

                CheckThreadId();

                _owner._lockSlim.ExitReadLock();
            }

            private void CheckThreadId()
            {
                if (Thread.CurrentThread.ManagedThreadId != _threadId)
                {
                    throw new InvalidOperationException("Synced list enumerator cannot be moved between threads.");
                }
            }
        }
    }
}