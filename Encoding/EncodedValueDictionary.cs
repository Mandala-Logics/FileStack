using System;
using System.Collections;
using System.Collections.Generic;

namespace MandalaLogics.Encoding
{
    public sealed class EncodedValueDictionary : IDictionary<string, EncodedValue>, IEncodable
    {
        public EncodedValue this[string key] { get => dict[key]; set => dict[key] = value; }
        public ICollection<string> Keys => dict.Keys;
        public ICollection<EncodedValue> Values => dict.Values;
        public int Count => dict.Count;
        public bool IsReadOnly => ((ICollection<KeyValuePair<string, EncodedValue>>)dict).IsReadOnly;

        private readonly Dictionary<string, EncodedValue> dict;

        public EncodedValueDictionary()
        {
            dict = new Dictionary<string, EncodedValue>(StringComparer.OrdinalIgnoreCase);
        }

        public EncodedValueDictionary(DecodingHandle handle)
        {
            int n = handle.Next<int>();

            dict = new Dictionary<string, EncodedValue>(n, StringComparer.OrdinalIgnoreCase);

            for (int x = 0; x < n; x++)
            {
                var key = handle.Next<string>();
                var val = handle.Next();

                dict.Add(key, val);
            }
        }

        public void Add(string key, EncodedValue value) => dict.Add(key, value);

        public void Add(string key, IEncodable value) => dict.Add(key, value.Encode());

        public void Add(string key, IConvertible value) => dict.Add(key, new EncodedPrimitive(value));

        public void Add(KeyValuePair<string, EncodedValue> item) => ((IDictionary<string, EncodedValue>)dict).Add(item);

        public void Clear() => dict.Clear();

        public bool Contains(KeyValuePair<string, EncodedValue> item) => ((IDictionary<string, EncodedValue>)dict).Contains(item);

        public bool ContainsKey(string key) => dict.ContainsKey(key);

        public void CopyTo(KeyValuePair<string, EncodedValue>[] array, int arrayIndex) => ((IDictionary<string, EncodedValue>)dict).CopyTo(array, arrayIndex);

        public IEnumerator<KeyValuePair<string, EncodedValue>> GetEnumerator() => dict.GetEnumerator();

        public bool Remove(string key) => dict.Remove(key);

        public bool Remove(KeyValuePair<string, EncodedValue> item) => ((IDictionary<string, EncodedValue>)dict).Remove(item);

        public bool TryGetValue(string key, out EncodedValue value) => dict.TryGetValue(key, out value);

        IEnumerator IEnumerable.GetEnumerator() => dict.GetEnumerator();

        void IEncodable.DoEncode(EncodingHandle handle)
        {
            handle.Append(dict.Count);

            foreach (var kvp in dict)
            {
                handle.Append(kvp.Key);
                handle.Append(kvp.Value);
            }
        }
    }
}