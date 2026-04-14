using System.Collections.Generic;

namespace MandalaLogics.Locking.Collection
{
    public sealed partial class ListInterface<T>
    {
        internal class ListClearAction : ListAction
        {
            protected override void DoPerformAction(IList<T> baseList)
            {
                baseList.Clear();
            }
        }
    }
    
}