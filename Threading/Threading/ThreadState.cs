using System;
using System.Threading;
using System.Threading.Tasks;
using MandalaLogics.Threading;

namespace MandalaLogics
{
    public class ThreadState
    {
        public bool NotYetStarted {get; private set;} = true;
        public bool FinishedRunning => res is object;
        public bool Started => !NotYetStarted;
        public bool Running {get; private set;}
        public bool Failed => res?.Failed ?? false;
        public bool Aborted => res?.Aborted ?? false;
        public ThreadResult Result
        {
            get
            {
                if (res is null) { throw new InvalidOperationException("Thread has not yet finished running or has not yet started; result not avalible."); }
                else { return (ThreadResult)res; }
            }
        }
        public ThreadBase Owner {get;}

        private ThreadResult? res;

        public ThreadState(ThreadBase owner)
        {
            Owner = owner;
        }

        internal void Reset()
        {
            NotYetStarted = true;
            res = null;
        }

        internal void StartRunning()
        {
            NotYetStarted = false;
            Running = true;
        }

        internal void SetResult(ThreadResult res)
        {
            this.res = res;
            Running = false;
            NotYetStarted = true;
        }

        public bool Wait(TimeSpan timeout)
        {
            if (FinishedRunning) { return true; }
            else if (NotYetStarted) { throw new InvalidOperationException("Thread has not yet started."); }

            if (timeout < TimeSpan.Zero) { throw new ArgumentException("Timeout cannot be less than zero."); }
            else if (timeout == TimeSpan.Zero) { return FinishedRunning; }

            if (Owner.ThreadID == Thread.CurrentThread.ManagedThreadId) { throw new CantJoinOwnThreadException(); }

            if (timeout.TotalMilliseconds > int.MaxValue) { timeout = TimeSpan.FromMilliseconds(int.MaxValue); }

            var ret = Owner.resetEvent.Wait(timeout);

            if (res?.Exception is { }) throw new AggregateException("Thread failed", res.Exception);
            else return ret;
        }

        public Task<ThreadResult> WaitAsync()
        {
            if (Owner.ThreadID == Thread.CurrentThread.ManagedThreadId) { throw new CantJoinOwnThreadException(); }

            return Owner.taskCompletionSource.Task;
        }
    }
}