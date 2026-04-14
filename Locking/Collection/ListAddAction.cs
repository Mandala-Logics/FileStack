using System.Collections.Generic;

namespace MandalaLogics.Locking.Collection
{
    public sealed partial class ListInterface<T>
    {
        internal class ListAddAction : ListAction
        {
            private readonly T _val;

            internal ListAddAction(T val)
            {
                _val = val;
            }
        
            protected override void DoPerformAction(IList<T> baseList)
            {
                baseList.Add(_val);
            }
        }
    }
    
}