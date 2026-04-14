using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using MandalaLogics.Encoding;

namespace MandalaLogics.Database
{
    public sealed partial class Yarn<TKey, TValue> : IDictionary<TKey, TValue> 
        where TKey : IConvertible where TValue : class, IEncodable
    {
        private const int MaxCacheSize = 128 * 1024;
        
        public TValue this[TKey key]
        {
            get => _cord[new YarnKey(key)];
            set => _cord.DoSet(new YarnKey(key), value);
        }

        public ICollection<TKey> Keys => new YarnKeyCollection(this);
        public ICollection<TValue> Values => _cord.Values;
        public int Count => _cord.Count;
        public bool IsReadOnly => false;

        private readonly Cord<YarnKey, TValue> _cord;

        static Yarn()
        {
            EncodingRegister.RegisterAll(Assembly.GetAssembly(typeof(YarnHandle)));
        }

        public Yarn(Stream stream, int maxCacheSize = MaxCacheSize)
        {
            _cord = new Cord<YarnKey, TValue>(stream, maxCacheSize);
        }

        public YarnHandle GetHandle(TKey key)
        {
            var yk = new YarnKey(key);
            
            return new YarnHandle(this, yk, _cord[yk]);
        }

        public void Add(KeyValuePair<TKey, TValue> item) => Add(item.Key, item.Value);

        public void Clear()
        {
            _cord.Clear();
        }

        public bool Contains(KeyValuePair<TKey, TValue> item)
        {
            var yk = new YarnKey(item.Key);

            if (_cord.TryGetValue(yk, out var val))
            {
                return val.Equals(item.Value);
            }

            return false;
        }

        public void CopyTo(KeyValuePair<TKey, TValue>[] array, int arrayIndex)
        {
            if (arrayIndex < 0) throw new ArgumentOutOfRangeException(nameof(arrayIndex));

            if (arrayIndex + Count > array.Length) throw new
                ArgumentException("The number of elements in the source ICollection<T> is greater " +
                                  "than the available space from arrayIndex to the end of the destination array.");

            var x = arrayIndex;

            foreach (var kvp in this)
            {
                array[x] = kvp;

                x++;
            }
        }

        public bool Remove(KeyValuePair<TKey, TValue> item)
        {
            var yk = new YarnKey(item.Key);
            
            if (_cord.TryGetValue(yk, out var val) && val.Equals(item.Value))
            {
                return _cord.Remove(yk);
            }

            return false;
        }
        
        public void Add(TKey key, TValue value)
        {
            var yk = new YarnKey(key);
            
            _cord.Add(yk, value);
        }

        public bool ContainsKey(TKey key)
        {
            var yk = new YarnKey(key);

            return _cord.ContainsKey(yk);
        }

        public bool Remove(TKey key)
        {
            var yk = new YarnKey(key);

            return _cord.Remove(yk);
        }

        public bool TryGetValue(TKey key, out TValue value)
        {
            var yk = new YarnKey(key);

            return _cord.TryGetValue(yk, out value);
        }

        public void Dispose() => _cord.Dispose();
        
        public IEnumerator<KeyValuePair<TKey, TValue>> GetEnumerator()
        {
            return new CastEnumerator<KeyValuePair<YarnKey, TValue>, KeyValuePair<TKey, TValue>>
                (_cord.GetEnumerator(), kvp =>
                {
                    if (typeof(TKey) != kvp.Key.Key.GetType())
                        throw new InvalidOperationException("The type of the key used in the generic argument is" +
                                                            "not the same type of key with which this Yarn was created.");
                    
                    return new KeyValuePair<TKey, TValue>((TKey)kvp.Key.Key, kvp.Value);
                });
        }

        IEnumerator IEnumerable.GetEnumerator()
        {
            return GetEnumerator();
        }
    }
}