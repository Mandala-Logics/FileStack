using System;
using System.Collections.Concurrent;
using System.Collections.Generic;

namespace MandalaLogics.Threading
{
    public sealed class MessageThreadPool<T>
    {
        private readonly ConcurrentQueue<T> messages = new ConcurrentQueue<T>();
        private readonly List<MessageLoopThread<T>> threads;
        private Action<Exception> ExceptionAction {get;}

        public Action<ThreadController, T> Action {get;}
        public int NumberOfThreads {get;}
        public bool Disposed {get; private set;} = false;

        public MessageThreadPool(Action<ThreadController, T> action, Action<Exception> exceptionAction, int numThreads = 5)
        {
            if (numThreads < 1 || numThreads > 24) { throw new ArgumentException("The number of threads must be between 1 and 24"); }

            Action = action;
            NumberOfThreads = numThreads;

            threads = new List<MessageLoopThread<T>>(numThreads);

            for (int x = 1; x <= numThreads; x++)
            {
                var mlt = new MessageLoopThread<T>(action, messages);
                threads.Add(mlt);

                mlt.ThreadComplete += ThreadComplete;

                mlt.Start();
            }

            ExceptionAction = exceptionAction;
        }

        private void ThreadComplete(ThreadBase sender, ThreadResult result)
        {
            if (result.Exception is Exception)
            {
                ExceptionAction.Invoke(result.Exception);

                sender.Start();
            }
        }

        public void Add(T msg)
        {
            if (Disposed) { throw new ObjectDisposedException("MessageThreadPool<T>"); }

            messages.Enqueue(msg);
        }

        public void Dispose()
        {
            if (Disposed) { return; }

            foreach (var mlt in threads)
            {
                mlt.AwaitAbort();
            }

            Disposed = true;
        }
    }
}