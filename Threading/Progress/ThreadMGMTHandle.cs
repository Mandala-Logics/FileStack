namespace MandalaLogics.Threading.Progress
{
    public sealed class ThreadMgmtHandle
    {
        internal static readonly ThreadMgmtHandle Null = new ThreadMgmtHandle(null);
        
        private readonly ThreadController? _controller;

        public bool IsAbortRequested => _controller?.IsAbortRequested ?? false;
        
        public ThreadMgmtHandle(ThreadController? tc)
        {
            _controller = tc;
        }
        
        public void ReportProgress(string text)
        {
            if (_controller is null) return;
            
            if (_controller?.SubProcessReportingLimit == ProgressLimitType.OnlyValue 
                || _controller?.SubProcessReportingLimit == ProgressLimitType.Nothing) return;
            
            _controller?.Progress.Report(text);
        }

        public void ReportProgress(string text, int value)
        {
            if (_controller is null) return;
            
            if (_controller.SubProcessReportingLimit == ProgressLimitType.Nothing) return;
            
            if (_controller.SubProcessReportingLimit == ProgressLimitType.OnlyValue)
            {
                _controller?.Progress.Report(value);
            }
            else
            {
                _controller?.Progress.Report(text);
            }
        }

        public void SetMaxProgress(int maxValue)
        {
            if (_controller is null) return;
            
            if (_controller.SubProcessReportingLimit == ProgressLimitType.OnlyText 
                || _controller.SubProcessReportingLimit == ProgressLimitType.Nothing) return;
            
            _controller.Progress.SetMax(maxValue);
        }

        public void ReportProgress(int value)
        {
            if (_controller is null) return;
            
            if (_controller.SubProcessReportingLimit == ProgressLimitType.OnlyText 
                || _controller?.SubProcessReportingLimit == ProgressLimitType.Nothing) return;
            
            _controller?.Progress.Report(value);
        }
    }
}