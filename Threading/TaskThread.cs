using System;

namespace MandalaLogics.Threading
{
    public sealed class TaskThread : ThreadBase
    {
        private readonly Action<ThreadController> _action;

        public TaskThread(Action<ThreadController> action)
        {
            _action = action;
        }

        protected override void ThreadAction(ThreadController tc)
        {
            _action.Invoke(tc);
        }
    }
}