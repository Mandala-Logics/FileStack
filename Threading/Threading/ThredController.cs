using System.Threading;

namespace MandalaLogics.Threading
{
    public class ThreadController
    {
        public static readonly ThreadController Null = new ThreadController(null);
        
        public bool IsAbortRequested => (cancellationToken?.IsCancellationRequested ?? false) || abortRequested;

        internal bool HasReturnValue {get; private set;} = false;
        internal object? ReturnValue {get; private set;} = null;

        private CancellationToken? cancellationToken;
        private volatile bool abortRequested = false;

        internal ThreadController(CancellationToken? cancellationToken)
        {
            this.cancellationToken = cancellationToken;
        }

        internal void SetCancelToken(CancellationToken token)
        {
            cancellationToken = token;
        }

        internal void Abort()
        {
            abortRequested = true;
        }

        internal void Reset()
        {
            abortRequested = false;
            HasReturnValue = false;
            ReturnValue = false;
            cancellationToken = null;
        }

        public void Return(object? value)
        {
            HasReturnValue = true;
            ReturnValue = value;
        }
    }
}