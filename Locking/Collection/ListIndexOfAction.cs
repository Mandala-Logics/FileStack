using System;
using System.Collections.Generic;

namespace MandalaLogics.Locking.Collection
{
    public sealed partial class ListInterface<T>
    {
        internal class ListIndexOfAction : ListAction
        {
            public int Result => _ret ?? throw new InvalidOperationException("Contains action has not been run yet.");
            
            private readonly T _val;
            private int? _ret = null;

            public ListIndexOfAction(T val)
            {
                _val = val;
            }

            protected override void DoPerformAction(IList<T> baseList)
            {
                _ret = baseList.IndexOf(_val);
            }
        }
    }
}
