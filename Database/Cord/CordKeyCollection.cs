using System;
using System.Collections;
using System.Collections.Generic;
using MandalaLogics.Encoding;

namespace MandalaLogics.Database
{
    public sealed partial class Cord<TKey, TValue>
    {
        internal class CordKeyCollection : ICollection<TKey>
        {
            public int Count => _owner.Count;
            public bool IsReadOnly => true;
            
            private readonly Cord<TKey, TValue> _owner;

            public CordKeyCollection(Cord<TKey, TValue> owner)
            {
                _owner = owner;
            }

            public void Add(TKey item) => throw new NotSupportedException();

            public void Clear() => throw new NotSupportedException();

            public bool Contains(TKey item)
            {
                var eo = item.Encode();

                var hash = eo.GetLongHash();
                
                var bucket = (int)(hash % BucketCount);
                
                _owner._bucketLock.Wait();

                try
                {
                    if (_owner._buckets[bucket] is null) return false;

                    foreach (var entry in _owner._buckets[bucket]!)
                    {
                        if (entry.Header.KeyHash == hash && entry.GetKey().Equals(item))
                            return true;
                    }

                    return false;
                }
                finally
                {
                    _owner._bucketLock.Release();
                }
            }

            public void CopyTo(TKey[] array, int arrayIndex)
            {
                if (arrayIndex < 0) throw new ArgumentOutOfRangeException(nameof(arrayIndex));

                if (arrayIndex + Count > array.Length) throw new
                    ArgumentException("The number of elements in the source ICollection<T> is greater " +
                                      "than the available space from arrayIndex to the end of the destination array.");

                int x = arrayIndex;

                foreach (var key in this)
                {
                    array[x] = key;

                    x++;
                }
            }

            public bool Remove(TKey item) => throw new NotSupportedException();
            
            public IEnumerator<TKey> GetEnumerator()
            {
                return new CordKeyEnumerator(_owner);
            }

            IEnumerator IEnumerable.GetEnumerator()
            {
                return GetEnumerator();
            }
        }
    }
}