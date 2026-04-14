using System;
using System.Collections;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Threading;

namespace MandalaLogics.Locking.Collection
{
    public sealed partial class ListInterface<T> : IList<T>, IDisposable where T : class
    {
        private static readonly TimeSpan WaitTime = TimeSpan.FromMilliseconds(50);

        public T this[int index]
        {
            get
            {
                var action = new ListGetAction(index);
                
                _actions.Add(action);

                action.Wait(TimeSpan.MaxValue);

                return action.Value;
            }
            
            set
            {
                var action = new ListSetAction(index, value);
                
                _actions.Add(action);

                action.Wait(TimeSpan.MaxValue);
            }
        }
        
        public int Count { get; private set; }
        public bool IsReadOnly => false;
        
        public bool Disposed { get; private set; } = false;
        
        private readonly BlockingCollection<ListAction> _actions 
            = new BlockingCollection<ListAction>(new ConcurrentQueue<ListAction>());
        
        private readonly IList<T> _baseList;
        private bool _disposing = false;
        
        private readonly Thread _loopThread;
        
        public ListInterface()
        {
            _baseList = new List<T>();
            
            _loopThread = new Thread(Loop);
            _loopThread.Start();
        }

        private void Loop()
        {
            while (!_disposing)
            {
                while (_actions.TryTake(out var task, WaitTime))
                {
                    task.PerformAction(_baseList);
                }
            }
            
            Disposed = true;
        }
        
        public void Dispose()
        {
            _disposing = true;

            _actions.CompleteAdding();

            _loopThread.Join();
        }

        public void Add(T item)
        {
            if (Disposed) throw new ObjectDisposedException(nameof(ListInterface<T>));
            
            var action = new ListAddAction(item);
            
            _actions.Add(action);

            try
            {
                action.Wait(TimeSpan.MaxValue);
            }
            finally
            {
                Count = action.Count;
            }
        }

        public void Clear()
        {
            if (Disposed) throw new ObjectDisposedException(nameof(ListInterface<T>));
            
            var action = new ListClearAction();
            
            _actions.Add(action);

            try
            {
                action.Wait(TimeSpan.MaxValue);
            }
            finally
            {
                Count = action.Count;
            }
        }

        public bool Contains(T item)
        {
            if (Disposed) throw new ObjectDisposedException(nameof(ListInterface<T>));
            
            var action = new ListContainsAction(item);
            
            _actions.Add(action);

            action.Wait(TimeSpan.MaxValue);

            return action.Result;
        }

        public void CopyTo(T[] array, int arrayIndex)
        {
            if (Disposed) throw new ObjectDisposedException(nameof(ListInterface<T>));
            
            if (arrayIndex < 0) throw new ArgumentOutOfRangeException(nameof(arrayIndex));

            var action = new ListCopyToAction(array, arrayIndex);
            
            _actions.Add(action);
            
            action.Wait(TimeSpan.MaxValue);
        }

        public int IndexOf(T item)
        {
            if (Disposed) throw new ObjectDisposedException(nameof(ListInterface<T>));
            
            var action = new ListIndexOfAction(item);
            
            _actions.Add(action);

            action.Wait(TimeSpan.MaxValue);

            return action.Result;
        }

        public bool Remove(T item)
        {
            if (Disposed) throw new ObjectDisposedException(nameof(ListInterface<T>));
            
            var action = new ListRemoveAction(item);
            
            _actions.Add(action);
            
            try
            {
                action.Wait(TimeSpan.MaxValue);
            }
            finally
            {
                Count = action.Count;
            }
            
            return action.Result;
        }
        
        public void Insert(int index, T item)
        {
            if (Disposed) throw new ObjectDisposedException(nameof(ListInterface<T>));
            
            var action = new ListInsertAction(index, item);
            
            _actions.Add(action);

            try
            {
                action.Wait(TimeSpan.MaxValue);
            }
            finally
            {
                Count = action.Count;
            }
        }

        public void RemoveAt(int index)
        {
            if (Disposed) throw new ObjectDisposedException(nameof(ListInterface<T>));
            
            var action = new ListRemoveAtAction(index);
            
            _actions.Add(action);

            try
            {
                action.Wait(TimeSpan.MaxValue);
            }
            finally
            {
                Count = action.Count;
            }
        }

        public IEnumerator<T> GetEnumerator()
        {
            if (Disposed) throw new ObjectDisposedException(nameof(ListInterface<T>));
            
            return new ThreadListEnumerator(this);
        }

        IEnumerator IEnumerable.GetEnumerator()
        {
            return GetEnumerator();
        }
    }
}