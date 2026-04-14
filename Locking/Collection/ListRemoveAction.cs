using System;
using System.Collections.Generic;

namespace MandalaLogics.Locking.Collection
{
    public sealed partial class ListInterface<T>
    {
        internal class ListRemoveAction : ListAction
        {
            public bool Result => _ret ?? throw new InvalidOperationException("Remove action has not been run yet.");
            
            private readonly T _val;
            private bool? _ret = null;

            public ListRemoveAction(T val)
            {
                _val = val;
            }

            protected override void DoPerformAction(IList<T> baseList)
            {
                _ret = baseList.Remove(_val);
            }
        }
    }
}
