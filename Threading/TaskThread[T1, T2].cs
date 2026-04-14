using System;

namespace MandalaLogics.Threading
{
    public class TaskThread<TArg1, TArg2> : ThreadBase
    {
        private readonly Action<ThreadController, TArg1, TArg2> _action;
        private readonly TArg1 _argument1;
        private readonly TArg2 _argument2;

        public TaskThread(Action<ThreadController, TArg1, TArg2> action, TArg1 argument1, TArg2 argument2)
        {
            _action = action;
            _argument1 = argument1;
            _argument2 = argument2;
        }

        protected override void ThreadAction(ThreadController tc)
        {
            _action.Invoke(tc, _argument1, _argument2);
        }
    }
}