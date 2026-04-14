using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace MandalaLogics.Locking.Collection
{
    public sealed partial class ListInterface<T>
    {
        internal abstract class ListAction
        {
            public int Count { get; private set; } = -1;
        
            public Task<ListAction> Task => _taskCompletionSource.Task;
        
            private readonly TaskCompletionSource<ListAction> _taskCompletionSource 
                = new TaskCompletionSource<ListAction>(TaskCreationOptions.RunContinuationsAsynchronously);
        
            internal ListAction() { }
        
            protected abstract void DoPerformAction(IList<T> baseList);

            internal void PerformAction(IList<T> baseList)
            {
                try
                {
                    DoPerformAction(baseList);
                    _taskCompletionSource.SetResult(this);
                }
                catch (Exception e)
                {
                    _taskCompletionSource.SetException(e);
                }
                finally
                {
                    Count = baseList.Count;
                }
            }
            
            public bool Wait(TimeSpan timeout)
            {
                if (timeout < TimeSpan.Zero) throw new ArgumentException("Timeout cannot be less than zero.");
            
                int ms;

                if (timeout.TotalMilliseconds > int.MaxValue) ms = int.MaxValue;
                else ms = (int)timeout.TotalMilliseconds;

                try
                {
                    return Task.Wait(ms);
                }
                catch (AggregateException e)
                {
                    throw e.InnerException!;
                }
            }
            
            public Task<ListAction> WaitAsync()
            {
                return Task;
            }
        }
    }
}