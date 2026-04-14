using System.Collections.Generic;

namespace MandalaLogics.Locking.Collection
{
    public sealed partial class ListInterface<T>
    {
        internal class ListInsertAction : ListAction
        {
            private readonly T _val;
            private readonly int _index;

            internal ListInsertAction(int index, T val)
            {
                _val = val;
                _index = index;
            }
        
            protected override void DoPerformAction(IList<T> baseList)
            {
                baseList.Insert(_index, _val);
            }
        }
    }
    
}