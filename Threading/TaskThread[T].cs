using System;

namespace MandalaLogics.Threading
{
    public class TaskThread<TArg> : ThreadBase
    {
        private readonly Action<ThreadController, TArg> _action;
        private readonly TArg _argument;

        public TaskThread(Action<ThreadController, TArg> action, TArg argument)
        {
            _action = action;
            _argument = argument;
        }

        protected override void ThreadAction(ThreadController tc)
        {
            _action.Invoke(tc, _argument);
        }
    }
}