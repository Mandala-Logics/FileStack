using System;
using System.Threading;
using System.Threading.Tasks;
using MandalaLogics.Threading;
using MandalaLogics.Threading.Progress;

namespace MandalaLogics
{
    public class ThreadState
    {
        public bool NotYetStarted {get; private set;} = true;
        public bool FinishedRunning => _res is object;
        public bool Started => !NotYetStarted;
        public bool Running {get; private set;}
        public bool Failed => _res?.Failed ?? false;
        public bool Aborted => _res?.Aborted ?? false;
        public ThreadResult Result
        {
            get
            {
                if (_res is null) { throw new InvalidOperationException("Thread has not yet finished running or has not yet started; result not avalible."); }
                else { return (ThreadResult)_res; }
            }
        }
        public ThreadBase Owner {get;}

        public IReadOnlyThreadProgress Progress { get; }
        
        internal readonly ThreadProgress Prog = new ThreadProgress();
        private ThreadResult? _res;

        public ThreadState(ThreadBase owner)
        {
            Owner = owner;

            Progress = new ThreadProgressReadOnlyWrapper(Prog);
        }

        internal void Reset()
        {
            NotYetStarted = true;
            _res = null;
            Prog.Reset();
        }

        internal void StartRunning()
        {
            NotYetStarted = false;
            Running = true;
        }

        internal void SetResult(ThreadResult res)
        {
            this._res = res;
            Running = false;
        }

        public bool Wait(TimeSpan timeout)
        {
            if (FinishedRunning) { return true; }
            else if (NotYetStarted) { throw new InvalidOperationException("Thread has not yet started."); }

            if (timeout < TimeSpan.Zero) { throw new ArgumentException("Timeout cannot be less than zero."); }
            else if (timeout == TimeSpan.Zero) { return FinishedRunning; }

            if (Owner.ThreadId == Thread.CurrentThread.ManagedThreadId) { throw new CantJoinOwnThreadException(); }

            if (timeout.TotalMilliseconds > int.MaxValue) { timeout = TimeSpan.FromMilliseconds(int.MaxValue); }

            var ret = Owner.ResetEvent.Wait(timeout);

            if (_res?.Exception is { }) throw new AggregateException("Thread failed", _res.Exception);
            else return ret;
        }

        public Task<ThreadResult> WaitAsync()
        {
            if (Owner.ThreadId == Thread.CurrentThread.ManagedThreadId) { throw new CantJoinOwnThreadException(); }

            return Owner.TaskCompletionSource.Task;
        }
    }
}