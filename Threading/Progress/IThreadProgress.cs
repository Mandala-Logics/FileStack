namespace MandalaLogics.Threading.Progress
{
    public interface IThreadProgress : IReadOnlyThreadProgress
    {
        public void Report(string text);
        public void Report(string text, int value);
        public void SetMax(int maxValue);
        public void Report(int value);
    }
}