using System;
using System.Collections.Generic;

namespace MandalaLogics.Database
{
    public sealed partial class Cord<TKey, TValue>
    {
        public class CordHandle : IDisposable
        {
            public Cord<TKey, TValue> Owner { get; }
            public TValue Value { get; set; }
            public TKey Key { get; }

            private bool _flushed = false;
            private bool _deleted = false;

            internal CordHandle(Cord<TKey, TValue> owner, TKey key, TValue value)
            {
                Owner = owner;
                Value = value;
                Key = key;
            }

            public void DeleteEntry()
            {
                Owner.Remove(Key);

                _deleted = true;
            }

            public void Flush()
            {
                if (_deleted) return;
                
                try
                {
                    Owner[Key] = Value;
                }
                catch (KeyNotFoundException)
                {
                    throw new InvalidOperationException("The key for this handle has been removed from the " +
                                                        "underlying dictionary.");
                }

                _flushed = true;
            }
            
            public void Dispose()
            {
                if (!_flushed) Flush();
            }
        }
    }
}