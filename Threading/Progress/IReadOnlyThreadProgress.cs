using System;

namespace MandalaLogics.Threading.Progress
{
    public interface IReadOnlyThreadProgress
    {
        public event EventHandler? ProgressUpdated;
        
        public string Text { get; }
        public int Value { get; }
        public int MaxValue { get; }
        public double Fraction { get; }
        public double Percent  { get; }
    }
}