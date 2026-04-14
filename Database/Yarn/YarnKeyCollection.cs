using System;
using System.Collections;
using System.Collections.Generic;

namespace MandalaLogics.Database
{
    public sealed partial class Yarn<TKey, TValue>
    {
        internal sealed class YarnKeyCollection : ICollection<TKey>
        {
            public int Count => _owner._cord.Count;
            public bool IsReadOnly => true;

            private readonly Yarn<TKey, TValue> _owner;
            private readonly ICollection<YarnKey> _baseCollection;

            public YarnKeyCollection(Yarn<TKey, TValue> owner)
            {
                _owner = owner;
                _baseCollection = owner._cord.Keys;
            }

            public void Add(TKey item) => throw new NotSupportedException();

            public void Clear() => throw new NotSupportedException();

            public bool Contains(TKey item)
            {
                var yk = new YarnKey(item);

                return _baseCollection.Contains(yk);
            }

            public void CopyTo(TKey[] array, int arrayIndex)
            {
                if (arrayIndex < 0) throw new ArgumentOutOfRangeException(nameof(arrayIndex));

                if (arrayIndex + Count > array.Length) throw new
                    ArgumentException("The number of elements in the source ICollection<T> is greater " +
                                      "than the available space from arrayIndex to the end of the destination array.");
                
                var x = arrayIndex;
            
                foreach (var key in this)
                {
                    array[x] = key;
                
                    x++;
                }
            }

            public bool Remove(TKey item) => throw new NotSupportedException();
            
            public IEnumerator<TKey> GetEnumerator()
            {
                return new CastEnumerator<YarnKey, TKey>(_baseCollection.GetEnumerator(),
                    yk =>
                    {
                        if (typeof(TKey) != yk.Key.GetType())
                            throw new InvalidOperationException("The type of the key used in the generic argument is" +
                                                                "not the same type of key with which this Yarn was created.");

                        return (TKey)yk.Key;
                    });
            }

            IEnumerator IEnumerable.GetEnumerator()
            {
                return GetEnumerator();
            }
        }
    }
}