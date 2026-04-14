using System.Collections.Generic;

namespace MandalaLogics.Locking.Collection
{
    public sealed partial class ListInterface<T>
    {
        internal class ListCopyToAction : ListAction
        {
            private readonly T[] _arr;
            private readonly int _arrayIndex;

            public ListCopyToAction(T[] array, int arrayIndex)
            {
                _arrayIndex = arrayIndex;
                _arr = array;
            }

            protected override void DoPerformAction(IList<T> baseList)
            {
                baseList.CopyTo(_arr, _arrayIndex);
            }
        }
    }
}
