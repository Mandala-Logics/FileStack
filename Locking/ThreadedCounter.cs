using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Threading;

namespace MandalaLogics.Locking
{
    public class ThreadedCounter
    {
        public int Count
        {
            get
            {
                lock (_gate)
                    return _counters.Count;
            }
        }
        
        private readonly Dictionary<Thread, int> _counters = new Dictionary<Thread, int>();
        private readonly object _gate = new object();

        public int Inc()
        {
            var t = Thread.CurrentThread;

            lock (_gate)
            {
                if (_counters.TryGetValue(t, out var c))
                {
                    return _counters[t] = c + 1;
                }
                else
                {
                    return _counters[t] = 1;
                }
            }
        }

        public int Dec()
        {
            var t = Thread.CurrentThread;

            lock (_gate)
            {
                if (!_counters.TryGetValue(t, out var c))
                    throw new InvalidOperationException("Current thread does not have a counter.");

                if (c == 1)
                {
                    _counters.Remove(t);
                    return 0;
                }

                return _counters[t] = c - 1;
            }
        }

        public int Get()
        {
            var t = Thread.CurrentThread;

            lock (_counters)
            {
                return _counters.GetValueOrDefault(t, 0);
            }
        }
    }
}