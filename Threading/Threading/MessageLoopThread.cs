using System;
using System.Collections.Concurrent;

namespace MandalaLogics.Threading
{
    public sealed class MessageLoopThread<T> : ThreadBase
    {
        public Action<ThreadController, T> Action {get;}

        private readonly BlockingCollection<T> messages;

        public MessageLoopThread(Action<ThreadController, T> action)
        {
            Action = action;
            messages = new BlockingCollection<T>(new ConcurrentQueue<T>());
        }

        internal MessageLoopThread(Action<ThreadController, T> action, IProducerConsumerCollection<T> collection)
        {
            Action = action;
            messages = new BlockingCollection<T>(collection);
        }

        public void Add(T msg)
        {
            messages.Add(msg);
        }

        protected override void ThreadAction(ThreadController tc)
        {
            while (!tc.IsAbortRequested)
            {
                if (messages.TryTake(out T msg, WaitTime))
                {
                    Action.Invoke(tc, msg);
                }
            }
        }
    }
}