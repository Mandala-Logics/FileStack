using System.Threading;
using MandalaLogics.Threading.Progress;

namespace MandalaLogics.Threading
{
    public class ThreadController
    {
        public static readonly ThreadController Null = new ThreadController(null, new ThreadProgress());
        
        public bool IsAbortRequested => (_cancellationToken?.IsCancellationRequested ?? false) || _abortRequested;

        internal bool HasReturnValue {get; private set;} = false;
        internal object? ReturnValue {get; private set;} = null;
        internal ProgressLimitType SubProcessReportingLimit { get; set; } = ProgressLimitType.Nothing;

        private CancellationToken? _cancellationToken;
        private volatile bool _abortRequested = false;
        public readonly ThreadProgress Progress;

        internal ThreadController(CancellationToken? cancellationToken, ThreadProgress progress)
        {
            _cancellationToken = cancellationToken;
            Progress = progress;
        }

        internal void SetCancelToken(CancellationToken token)
        {
            _cancellationToken = token;
        }

        internal void Abort()
        {
            _abortRequested = true;
        }

        internal void Reset()
        {
            _abortRequested = false;
            HasReturnValue = false;
            ReturnValue = false;
            _cancellationToken = null;
        }

        public void Return(object? value)
        {
            HasReturnValue = true;
            ReturnValue = value;
        }
    }
}