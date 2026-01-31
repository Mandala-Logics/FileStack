using System;
using System.Collections.Generic;
using System.Linq;
using MandalaLogics.Encoding;
using MandalaLogics.Weave;

namespace MandalaLogics.Splice
{
    internal class SpliceCache
    {
        private Dictionary<int, CachedEncodable> _cache = new Dictionary<int, CachedEncodable>();
        
        private readonly int _maxSize;
        private int _size = 0;
        
        public SpliceCache(int maxSize)
        {
            _maxSize = maxSize;
        }
        
        public bool TrySet(int index, IEncodable value, int size)
        {
            if (_cache.TryGetValue(index, out var ce))
            {
                var x = _size - ce.Size + size;

                if (x > _maxSize) //new size would be too big
                {
                    _cache.Remove(index);
                    return false;
                }
                else
                {
                    _size = x;

                    _cache[index] = new CachedEncodable(size, value);

                    return true;
                }
            }
            else
            {
                var x = size + _size;

                if (x > _maxSize) return false;

                _size = x;
                
                _cache.Add(index, new CachedEncodable(size, value));

                return true;
            }
        }
        
        public void Remove(int index)
        {
            if (_cache.Remove(index, out var ce))
            {
                _size -= ce.Size;
            }
            
        }
        
        public IEncodable? TryGet(int index)
        {
            return _cache.TryGetValue(index, out var ce) ? ce.Value : null;
        }

        public IEncodable Get(int index)
        {
            return _cache[index].Value;
        }

        public int? IndexOf(IEncodable val)
        {
            foreach (var kvp in _cache)
            {
                if (kvp.Value.Value.Equals(val)) return kvp.Key;
            }

            return null;
        }

        public void Foreach<T>(Action<int, T> action) where T : IEncodable
        {
            foreach (var kvp in _cache)
            {
                action.Invoke(kvp.Key, (T)kvp.Value.Value);
            }
        }

        public int? IndexOf<T>(Predicate<T> predicate) where T : IEncodable
        {
            foreach (var kvp in _cache)
            {
                if (predicate.Invoke((T)kvp.Value.Value)) return kvp.Key;
            }

            return null;
        }

        public bool OnFirst<T>(Predicate<T> predicate, Action<int, T> action)
        {
            foreach (var kvp in _cache)
            {
                if (predicate.Invoke((T)kvp.Value.Value))
                {
                    action.Invoke(kvp.Key, (T)kvp.Value.Value);
                    return true;
                }
            }

            return false;
        }

        public bool Contains(IEncodable item) => _cache.Values.Any(ce => ce.Value.Equals(item));

        public bool Contains(int index) => _cache.ContainsKey(index);

        public void Clear()
        {
            _cache.Clear();

            _size = 0;
        }
    }
}