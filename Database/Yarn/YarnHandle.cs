using System;
using System.Collections.Generic;

namespace MandalaLogics.Database
{
    public sealed partial class Yarn<TKey, TValue>
    {
        public class YarnHandle : IDisposable
        {
            public Yarn<TKey, TValue> Owner { get; }
            
            public TValue Value { get; set; }
            public TKey Key => (TKey)_key.Key;
            
            private readonly YarnKey _key;
            private bool _flushed = false;
            private bool _deleted = false;

            public YarnHandle(Yarn<TKey, TValue> owner, YarnKey key, TValue value)
            {
                Owner = owner;
                _key = key;
                Value = value;
            }
            
            public void DeleteEntry()
            {
                Owner._cord.Remove(_key);

                _deleted = true;
            }

            public void Flush()
            {
                if (_deleted) return;
                
                try
                {
                    Owner._cord[_key] = Value;
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