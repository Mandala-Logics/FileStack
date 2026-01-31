using System;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;

namespace MandalaLogics.Threading
{
    public delegate void ThreadCallbackDelegate(ThreadBase sender, ThreadResult result);

    public abstract class ThreadBase
    {
        public static readonly TimeSpan WaitTime = TimeSpan.FromMilliseconds(500);

        public event ThreadCallbackDelegate? ThreadComplete;

        public ThreadState State {get;}

        internal int ThreadID => mainThread.ManagedThreadId;
        private Thread mainThread = null!;

        internal readonly ThreadController Controller;
        internal TaskCompletionSource<ThreadResult> taskCompletionSource = null!;
        internal ManualResetEventSlim resetEvent = new ManualResetEventSlim(false);

        protected ThreadBase()
        {
            State = new ThreadState(this);

            Controller = new ThreadController(null);
        }

        public void Start(CancellationToken cancellationToken)
        {
            if (State.Running) { throw new InvalidOperationException("Thread is already running."); }

            Controller.SetCancelToken(cancellationToken);

            mainThread = new Thread(RunAction);

            taskCompletionSource = new TaskCompletionSource<ThreadResult>();
            resetEvent.Reset();
            State.Reset();
            State.StartRunning();

            mainThread.Start();
        }

        public void Start()
        {
            if (State.Running) { throw new InvalidOperationException("Thread is already running."); }

            mainThread = new Thread(RunAction);

            taskCompletionSource = new TaskCompletionSource<ThreadResult>();
            resetEvent.Reset();
            State.Reset();
            State.StartRunning();

            mainThread.Start();
        }

        public void Join()
        {
            State.Wait(TimeSpan.MaxValue);
        }

        public bool Join(TimeSpan timeout)
        {
            return State.Wait(timeout);
        }

        public Task<ThreadResult> JoinAsync()
        {
            return State.WaitAsync();
        }

        public void Abort()
        {
            if (State.NotYetStarted) { throw new InvalidOperationException("Thread has not started yet."); }

            Controller.Abort();
        }

        public bool AwaitAbort(TimeSpan timeout)
        {
            if (State.NotYetStarted) { throw new InvalidOperationException("Thread has not started yet."); }

            Controller.Abort();

            return State.Wait(timeout);
        }

        public bool AwaitAbort()
        {
            if (State.NotYetStarted) { throw new InvalidOperationException("Thread has not started yet."); }

            Controller.Abort();
            
            return State.Wait(TimeSpan.MaxValue);
        }

        public Task<ThreadResult> AwaitAbortAsync()
        {
            if (State.NotYetStarted) { throw new InvalidOperationException("Thread has not started yet."); }

            Controller.Abort();
            
            return State.WaitAsync();
        }

        protected abstract void ThreadAction(ThreadController tc);

        protected void RunAction()
        {
            try
            {   
                
                
                if (Controller.IsAbortRequested)
                {
                    State.SetResult(new ThreadResult(true));
                }
    
                try
                {
                    ThreadAction(Controller);

                    State.SetResult(new ThreadResult(Controller.IsAbortRequested));

                    if (Controller.HasReturnValue) { State.Result.SetReturnValue(Controller.ReturnValue); }

                    taskCompletionSource.SetResult(State.Result);
                }
                catch (TargetInvocationException e)
                {
                    State.SetResult(new ThreadResult(e.InnerException));
                    taskCompletionSource.SetException(e.InnerException);
                }
                catch (Exception e)
                {
                    State.SetResult(new ThreadResult(e));
                    taskCompletionSource.SetException(e);
                }
            }
            finally
            {
                Controller.Reset();
                resetEvent.Set();
            }

            ThreadComplete?.Invoke(this, State.Result);
        }
    }
}