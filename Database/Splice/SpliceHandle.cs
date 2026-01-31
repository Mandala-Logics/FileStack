using System;

namespace MandalaLogics.Splice
{
    public sealed partial class Splice<T>
    {
        public sealed class SpliceHandle : IDisposable
        {
            public T Value { get; }
            public Splice<T> Owner { get; }

            private readonly int _blockIndex;
            private bool _deleted = false;

            internal SpliceHandle(Splice<T> owner, int chainIndex)
            {
                Owner = owner;
                
                var chain = Owner._chains[chainIndex];
                _blockIndex = chain.BlockIndex;

                if (Owner._cache.TryGet(chainIndex) is { } ie)
                {
                    Value = (T)ie;
                }
                else
                {
                    Value = Owner.ReadFromChain(chainIndex);
                }
            }

            public void DeleteEntry()
            {
                Owner.EnterWriteLock();
                
                try
                {
                    if (Owner._blocks[_blockIndex] is ChainHeader ch)
                    {
                        Owner.DestroyChain(ch.Ordinal);
                        Owner._cache.Remove(ch.Ordinal);
                        _deleted = true;
                    }
                }
                finally
                {
                    Owner._enumLock.ExitWriteLock();
                    Owner._streamLock.ExitWriteLock();
                }
            }

            public void Flush()
            {
                if (_deleted) return;
                
                Owner.EnterWriteLock();
                
                try
                {
                    if (Owner._blocks[_blockIndex] is ChainHeader ch)
                    {
                        Owner.WriteToChain(ch.Ordinal, Value);
                    }
                    else
                    {
                        throw new
                            InvalidOperationException("The underlying value has been removed " +
                                                      "from the Splice while this handle was open.");
                    }
                }
                finally
                {
                    Owner._enumLock.ExitWriteLock();
                    Owner._streamLock.ExitWriteLock();
                }
            }

            public void Dispose()
            {
                Flush();
            }
        } 
    }
}