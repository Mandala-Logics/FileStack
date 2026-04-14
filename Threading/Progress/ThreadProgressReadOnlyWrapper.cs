using System;

namespace MandalaLogics.Threading.Progress
{
    internal class ThreadProgressReadOnlyWrapper : IReadOnlyThreadProgress
    {
        private readonly ThreadProgress _progress;

        internal ThreadProgressReadOnlyWrapper(ThreadProgress progress)
        {
            _progress = progress;
        }

        public event EventHandler? ProgressUpdated
        {
            add => _progress.ProgressUpdated += value;
            remove => _progress.ProgressUpdated -= value;
        }

        public string Text => _progress.Text;

        public int Value => _progress.Value;

        public int MaxValue => _progress.MaxValue;

        public double Fraction => _progress.Fraction;

        public double Percent => _progress.Percent;
    }
}