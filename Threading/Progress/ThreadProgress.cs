using System;

namespace MandalaLogics.Threading.Progress
{
    public sealed class ThreadProgress : IThreadProgress, IProgress<int>
    {
        public event EventHandler? ProgressUpdated;
        
        public string Text { get; private set; } = string.Empty;
        public int Value { get; private set; } = 0;
        public int MaxValue { get; private set; } = int.MaxValue;
        public double Fraction => Value / (double)MaxValue;
        public double Percent => Fraction * 100d;
        
        internal ThreadProgress() {}

        internal void Reset()
        {
            Value = 0;
            Text = string.Empty;
            MaxValue = int.MaxValue;
        }

        public void Report(string text)
        {
            Text = text;
            ProgressUpdated?.Invoke(this, EventArgs.Empty);
        }

        public void Report(string text, int value)
        {
            Text = text;
            Value = value;
            ProgressUpdated?.Invoke(this, EventArgs.Empty);
        }

        public void SetMax(int maxValue)
        {
            MaxValue = maxValue;
        }

        public void Report(int value)
        {
            Value = value;
            ProgressUpdated?.Invoke(this, EventArgs.Empty);
        }
        
        public IThreadProgress Limit(ProgressLimitType type)
        {
            return new ThreadProgressLimitWrapper(this, type);
        }
    }
}