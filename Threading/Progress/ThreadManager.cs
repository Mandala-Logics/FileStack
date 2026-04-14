using System;
using System.Collections.Generic;
using System.Threading;

namespace MandalaLogics.Threading.Progress
{
    public static class ThreadManager
    {
        private static Thread? _mainThread;
        private static readonly Dictionary<Thread, ThreadController> Threads = new Dictionary<Thread, ThreadController>();
        
        internal static void Init()
        {
            if (_mainThread is { }) return;

            _mainThread = Thread.CurrentThread;
        }

        internal static void AddController(ThreadController tc)
        {
            if (_mainThread is null)
                throw new InvalidOperationException("Class has not been initialised.");
            
            Threads.Add(Thread.CurrentThread, tc);
        }

        internal static void RemoveController()
        {
            if (_mainThread is null)
                throw new InvalidOperationException("Class has not been initialised.");
            
            if (!Threads.Remove(Thread.CurrentThread))
                throw new InvalidOperationException("This thread was not added.");
        }

        public static bool AmIOnMainThread()
        {
            if (_mainThread is null)
                throw new InvalidOperationException("Class has not been initialised.");
            
            return Thread.CurrentThread.Equals(_mainThread);
        }
        
        public static bool AmIOnThread()
        {
            if (_mainThread is null)
                throw new InvalidOperationException("Class has not been initialised.");
            
            return !Thread.CurrentThread.Equals(_mainThread);
        }

        public static ThreadMgmtHandle GetThreadHandle()
        {
            if (_mainThread is null)
                throw new InvalidOperationException("Class has not been initialised.");
            
            if (Threads.TryGetValue(Thread.CurrentThread, out var controller))
            {
                return new ThreadMgmtHandle(controller);
            }
            else
            {
                return ThreadMgmtHandle.Null;
            }
        }
    }
}