using System;
using System.Collections.Concurrent;

namespace MandalaLogics.Threading
{
    public sealed class MessageLoopThread<T> : ThreadBase
    {
        public Action<ThreadController, T> Action {get;}

        public TimeSpan CurrentExecutionTime => 
            _lastStartTime is null ? TimeSpan.Zero : DateTime.Now - (DateTime)_lastStartTime;

        private readonly BlockingCollection<T> _messages;
        private DateTime? _lastStartTime;

        public MessageLoopThread(Action<ThreadController, T> action)
        {
            Action = action;
            _messages = new BlockingCollection<T>(new ConcurrentQueue<T>());
        }

        internal MessageLoopThread(Action<ThreadController, T> action, IProducerConsumerCollection<T> collection)
        {
            Action = action;
            _messages = new BlockingCollection<T>(collection);
        }

        public void Add(T msg)
        {
            _messages.Add(msg);
        }

        public bool TryTakeMessage(out T msg, TimeSpan waitTime)
        {
            return _messages.TryTake(out msg, WaitTime);
        }

        protected override void ThreadAction(ThreadController tc)
        {
            while (!tc.IsAbortRequested)
            {
                if (_messages.TryTake(out T msg, WaitTime))
                {
                    _lastStartTime = DateTime.Now;
                    Action.Invoke(tc, msg);
                    _lastStartTime = null;
                }
            }
        }
    }
}