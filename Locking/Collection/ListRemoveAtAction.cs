using System.Collections.Generic;

namespace MandalaLogics.Locking.Collection
{
    public sealed partial class ListInterface<T>
    {
        internal class ListRemoveAtAction : ListAction
        {
            private readonly int _index;

            public ListRemoveAtAction(int index)
            {
                _index = index;
            }

            protected override void DoPerformAction(IList<T> baseList)
            {
                baseList.RemoveAt(_index);
            }
        }
    }
}
