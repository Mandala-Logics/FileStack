using System;
using System.Collections;
using System.Collections.Generic;
using System.Threading;

namespace MandalaLogics.Packing
{
    public sealed partial class Ribbon
    {
        public class RibbonEnumerator : IEnumerator<uint>
        {
            public uint Current { get; private set; }

            object? IEnumerator.Current => _baseEnum.Current;
            private readonly Ribbon _owner;

            private IEnumerator<uint> _baseEnum = null!;
            
            private readonly int _threadId;
            
            public RibbonEnumerator(Ribbon owner)
            {
                _owner = owner;
                
                _threadId = Thread.CurrentThread.ManagedThreadId;
                
                Reset();
            }

            public bool MoveNext()
            {
                CheckThreadId();
                
                return _baseEnum.MoveNext();
            }

            public void Reset()
            {
                CheckThreadId();
                
                lock (this)
                {
                    if (!_owner._enumLock.IsReadLockHeld) _owner._enumLock.EnterReadLock();
                }
                
                _baseEnum?.Dispose();
                _baseEnum = _owner._strands.GetEnumerator();
            }

            public void Dispose()
            {
                CheckThreadId();
                
                _baseEnum?.Dispose();

                lock (this)
                {
                    if (!_owner._enumLock.IsReadLockHeld) return;
                    
                    _owner._enumLock.ExitReadLock();
                }
            }
            
            private void CheckThreadId()
            {
                if (Thread.CurrentThread.ManagedThreadId != _threadId)
                {
                    throw new InvalidOperationException("Splice enumerator cannot be moved between threads.");
                }
            }
        }
    }
}