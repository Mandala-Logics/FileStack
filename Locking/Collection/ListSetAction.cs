using System.Collections.Generic;

namespace MandalaLogics.Locking.Collection
{
    public sealed partial class ListInterface<T>
    {
        internal class ListSetAction : ListAction
        {
            private readonly T _val;
            private readonly int _index;

            public ListSetAction(int index, T val)
            {
                _index = index;
                _val = val;
            }

            protected override void DoPerformAction(IList<T> baseList)
            {
                baseList[_index] = _val;
            }
        }
    }
}
