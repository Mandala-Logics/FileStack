using System;
using System.Collections.Generic;
using System.Threading;

public sealed class EntryTracker<T> where T : notnull
{
    private readonly object _gate = new object();
    private readonly Dictionary<T, SemaphoreSlim> _locks = new Dictionary<T, SemaphoreSlim>();

    public int Count { get { lock (_gate) return _locks.Count; } }

    public bool EnterLock(T key, TimeSpan timeout)
    {
        SemaphoreSlim sem;
        lock (_gate)
        {
            if (!_locks.TryGetValue(key, out sem))
            {
                sem = new SemaphoreSlim(1, 1);
                _locks.Add(key, sem);
            }
        }

        return sem.Wait(timeout);
    }

    public void EnterLock(T key)
    {
        SemaphoreSlim sem;
        lock (_gate)
        {
            if (!_locks.TryGetValue(key, out sem))
            {
                sem = new SemaphoreSlim(1, 1);
                _locks.Add(key, sem);
            }
        }

        sem.Wait();
    }

    public void ExitLock(T key)
    {
        SemaphoreSlim? sem;
        lock (_gate)
            _locks.TryGetValue(key, out sem);
        
        sem?.Release();
    }

    public bool IsLocked(T key)
    {
        lock (_gate) return _locks.ContainsKey(key);
    }
}