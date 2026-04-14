using System.Collections.Generic;

namespace MandalaLogics.Locking.Collection
{
    public sealed partial class ListInterface<T>
    {
        internal class ListGetAction : ListAction
        {
            public T Value => _val ?? throw new ProgrammerException("Get task is not complete yet.");
            
            private T? _val = null;
            private readonly int _index;

            public ListGetAction(int index)
            {
                _index = index;
            }

            protected override void DoPerformAction(IList<T> baseList)
            {
                _val = baseList[_index];
            }
        }
    }
}
