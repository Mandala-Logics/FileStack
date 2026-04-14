using System;
using System.Collections;
using System.Collections.Generic;
using MandalaLogics.Encoding;

namespace MandalaLogics.Database
{
    public sealed partial class Cord<TKey, TValue>
    {
        internal class CordValueCollection : ICollection<TValue>
        {
            public int Count => _owner.Count;
            public bool IsReadOnly => true;

            private readonly Cord<TKey, TValue> _owner;

            public CordValueCollection(Cord<TKey, TValue> owner)
            {
                _owner = owner;
            }

            public void Add(TValue item) => throw new NotSupportedException();

            public void Clear() => throw new NotSupportedException();

            public bool Contains(TValue item)
            {
                var eo = item.Encode();

                var hash = eo.GetLongHash();

                _owner._bucketLock.Wait();

                try
                {
                    foreach (var bucket in _owner._buckets)
                    {
                        if (bucket is null) continue;
                        
                        foreach (var entry in bucket)
                        {
                            if (entry.Header.ValueHash == hash && entry.GetValue().Equals(item))
                                return true;
                        }
                    }

                    return false;
                }
                finally
                {
                    _owner._bucketLock.Release();
                }
            }

            public void CopyTo(TValue[] array, int arrayIndex)
            {
                if (arrayIndex < 0) throw new ArgumentOutOfRangeException(nameof(arrayIndex));

                if (arrayIndex + Count > array.Length) throw new
                    ArgumentException("The number of elements in the source ICollection<T> is greater " +
                                      "than the available space from arrayIndex to the end of the destination array.");

                var x = arrayIndex;

                foreach (var val in this)
                {
                    array[x] = val;

                    x++;
                }
            }

            public bool Remove(TValue item) => throw new NotSupportedException();

            public IEnumerator<TValue> GetEnumerator()
            {
                return new CastEnumerator<KeyValuePair<TKey, TValue>, TValue>
                    (_owner.GetEnumerator(), pair => pair.Value);
            }

            IEnumerator IEnumerable.GetEnumerator()
            {
                return GetEnumerator();
            }
        }
    }
}