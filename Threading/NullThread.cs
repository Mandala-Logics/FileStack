namespace MandalaLogics.Threading
{
    public class NullThread : ThreadBase
    {
        public static ThreadBase CompletedThread { get; }
        
        static NullThread()
        {
            CompletedThread = new NullThread();
            CompletedThread.Start();
            CompletedThread.Join();
        }
        
        private NullThread()
        {
        }

        protected override void ThreadAction(ThreadController tc)
        {
        }
    }
}