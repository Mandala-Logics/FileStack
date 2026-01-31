using System;
using System.Collections;
using System.Collections.Generic;

namespace MandalaLogics.Path
{
    internal sealed class ReadOnlyListWrapper<T> : IReadOnlyList<T>
    {
        //PRIVATE PROPERTIES
        private IReadOnlyList<T> baseList;

        //PUBLIC PROPERTIES
        public T this[int index] => baseList[index];
        public int Count => baseList.Count;

        //CONSTRCUTORS
        internal ReadOnlyListWrapper(IReadOnlyList<T> baseList)
        {
            this.baseList = baseList ?? throw new ArgumentNullException(nameof(baseList));
        }

        //PUBLIC METHODS
        public IEnumerator<T> GetEnumerator() => baseList.GetEnumerator();
        IEnumerator IEnumerable.GetEnumerator() => baseList.GetEnumerator();
    }
}