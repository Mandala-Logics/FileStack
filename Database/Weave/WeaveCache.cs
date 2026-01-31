using System.Collections.Generic;
using MandalaLogics.Encoding;

namespace MandalaLogics.Weave
{
    internal class WeaveCache
    {
        private Dictionary<int, CachedEncodable> _cache = new Dictionary<int, CachedEncodable>();

        private readonly int _maxSize;
        private int _size = 0;
        
        public WeaveCache(int maxSize)
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
            
            var tmp = new Dictionary<int, CachedEncodable > (_cache.Count);

            foreach (var kvp in _cache)
            {
                if (kvp.Key > index)
                {
                    tmp.Add(kvp.Key - 1, kvp.Value); //decrement any entries with and index higher than the removed index
                }
                else
                {
                    tmp.Add(kvp.Key, kvp.Value);
                }
            }

            _cache = tmp;
        }

        public IEncodable? TryGet(int index)
        {
            return _cache.TryGetValue(index, out var ce) ? ce.Value : null;
        }

        public IEncodable Get(int index)
        {
            return _cache[index].Value;
        }

        public bool Contains(int index) => _cache.ContainsKey(index);

        public void Clear()
        {
            _cache.Clear();

            _size = 0;
        }
    }
}