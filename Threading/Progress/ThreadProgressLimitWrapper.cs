using System;

namespace MandalaLogics.Threading.Progress
{
    public enum ProgressLimitType { Nothing, OnlyText, OnlyValue }
    
    internal class ThreadProgressLimitWrapper : IThreadProgress
    {
        public event EventHandler? ProgressUpdated;

        public string Text => _progress.Text;
        public int Value => _progress.Value;
        public int MaxValue => _progress.MaxValue;
        public double Fraction => _progress.Fraction;
        public double Percent => _progress.Percent;

        private readonly IThreadProgress _progress;
        private readonly ProgressLimitType _limitType;

        public ThreadProgressLimitWrapper(IThreadProgress progress, ProgressLimitType type)
        {
            _progress = progress;
            _limitType = type;
        }
        
        public void Report(string text)
        {
            if (_limitType == ProgressLimitType.OnlyValue || _limitType == ProgressLimitType.Nothing) return;
            
            _progress.Report(text);
        }

        public void Report(string text, int value)
        {
            if (_limitType == ProgressLimitType.Nothing) return;
            
            if (_limitType == ProgressLimitType.OnlyValue)
            {
                Report(value);
            }
            else
            {
                Report(text);
            }
        }

        public void SetMax(int maxValue)
        {
            if (_limitType == ProgressLimitType.OnlyText || _limitType == ProgressLimitType.Nothing) return;
            
            _progress.SetMax(maxValue);
        }

        public void Report(int value)
        {
            if (_limitType == ProgressLimitType.OnlyText || _limitType == ProgressLimitType.Nothing) return;
            
            _progress.Report(value);
        }
    }
}