using System;

namespace MandalaLogics.Threading
{
    public class ThreadResult
    {
        public bool Failed => Exception is Exception;
        public Exception? Exception {get;}
        public bool Success => !Aborted && !Failed;
        public bool Aborted {get;}
        public object? ReturnValue
        {
            get
            {
                if (!HasReturnValue) { throw new InvalidOperationException("Thread did not return a value."); }
                else { return returnValue; }
            }
        }
        public bool HasReturnValue {get; private set;}

        private object? returnValue;

        internal ThreadResult(bool aborted)
        {
            Aborted = aborted;
            Exception = null;
            returnValue = null;
            HasReturnValue = false;
        }

        internal ThreadResult(Exception e)
        {
            Exception = e;
            Aborted = false;
            returnValue = null;
            HasReturnValue = false;
        }

        internal void SetReturnValue(object? value)
        {
            HasReturnValue = true;
            returnValue = value;
        }
    }
}