using System;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;
using MandalaLogics.Threading.Progress;

namespace MandalaLogics.Threading
{
    public delegate void ThreadCallbackDelegate(ThreadBase sender, ThreadResult result);

    public abstract class ThreadBase
    {
        public static readonly TimeSpan WaitTime = TimeSpan.FromMilliseconds(500);

        public event ThreadCallbackDelegate? ThreadComplete;

        public ThreadState State {get;}

        internal int ThreadId => _mainThread.ManagedThreadId;
        private Thread _mainThread = null!;

        internal readonly ThreadController Controller;
        internal TaskCompletionSource<ThreadResult> TaskCompletionSource = null!;
        internal readonly ManualResetEventSlim ResetEvent = new ManualResetEventSlim(false);

        protected ThreadBase()
        {
            State = new ThreadState(this);

            Controller = new ThreadController(null, State.Prog);
            
            ThreadManager.Init();
        }

        public void Start(CancellationToken cancellationToken)
        {
            if (State.Running) { throw new InvalidOperationException("Thread is already running."); }

            Controller.SetCancelToken(cancellationToken);

            _mainThread = new Thread(RunAction);

            TaskCompletionSource = new TaskCompletionSource<ThreadResult>();
            ResetEvent.Reset();
            State.Reset();
            State.StartRunning();

            _mainThread.Start();
        }

        public void Start()
        {
            if (State.Running) { throw new InvalidOperationException("Thread is already running."); }

            _mainThread = new Thread(RunAction);

            TaskCompletionSource = new TaskCompletionSource<ThreadResult>();
            ResetEvent.Reset();
            State.Reset();
            State.StartRunning();

            _mainThread.Start();
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

        private void RunAction()
        {
            try
            {   
                ThreadManager.AddController(Controller);
                
                if (Controller.IsAbortRequested)
                {
                    State.SetResult(new ThreadResult(true));
                }
    
                try
                {
                    ThreadAction(Controller);

                    State.SetResult(new ThreadResult(Controller.IsAbortRequested));

                    if (Controller.HasReturnValue) { State.Result.SetReturnValue(Controller.ReturnValue); }

                    TaskCompletionSource.SetResult(State.Result);
                }
                catch (TargetInvocationException e)
                {
                    State.SetResult(new ThreadResult(e.InnerException));
                    TaskCompletionSource.SetException(e.InnerException);
                }
                catch (Exception e)
                {
                    State.SetResult(new ThreadResult(e));
                    TaskCompletionSource.SetException(e);
                }
            }
            finally
            {
                ThreadManager.RemoveController();
                Controller.Reset();
                ResetEvent.Set();
            }

            ThreadComplete?.Invoke(this, State.Result);
        }
    }
}