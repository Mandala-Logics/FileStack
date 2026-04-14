using System;
using System.Collections.Concurrent;
using System.Collections.Generic;

namespace MandalaLogics.Locking
{
    public class Leaser<TKey, TValue> where TValue : class, ILeaseable<TValue>
    {
        private readonly ConcurrentDictionary<TKey, TValue> _dict = new ConcurrentDictionary<TKey, TValue>();

        public TValue Get(TKey key)
        {
            if (_dict.TryGetValue(key, out var val))
            {
                return val;
            }
            else
            {
                throw new KeyNotFoundException();
            }
        }

        public bool TryGet(TKey key, out TValue val)
        {
            if (_dict.TryGetValue(key, out var x))
            {
                val = x;
                return true;
            }
            else
            {
                val = null!;
                return false;
            }
        }
        
        public bool TryAdd(TKey key, TValue val)
        {
            return _dict.TryAdd(key, val);
        }

        public Lease<TValue> TakeLease(TKey key)
        {
            if (_dict.TryGetValue(key, out var val))
            {
                return val.GetLease();
            }
            else
            {
                throw new KeyNotFoundException();
            }
        }

        public bool Remove(TKey key)
        {
            return _dict.Remove(key, out _);
        }

        public bool ContainsKey(TKey key)
        {
            return _dict.ContainsKey(key);
        }

        public bool TryTakeLease(TKey key, out Lease<TValue> lease)
        {
            if (_dict.TryGetValue(key, out var val))
            {
                lease = val.GetLease();
                return true;
            }
            else
            {
                lease = null!;
                return false;
            }
        }

        public Lease<TValue> AddAndTakeLease(TKey key, TValue val)
        {
            if (_dict.TryAdd(key, val))
            {
                return val.GetLease();
            }
            else
            {
                throw new InvalidOperationException("Key already exists.");
            }
        }

        public void Clear()
        {
            _dict.Clear();
        }
    }
}