using System;
using System.Collections;
using System.Collections.Generic;

namespace MandalaLogics.Locking.Collection
{
    public sealed partial class ListInterface<T>
    {
        internal class ThreadListEnumerator : IEnumerator<T>
        {
            public T Current { get; private set; } = null!;

            object? IEnumerator.Current => Current;

            private readonly ListInterface<T> _owner;
            private int _pos = -1;
            private bool _done = false;

            public ThreadListEnumerator(ListInterface<T> owner)
            {
                _owner = owner;
            }
            
            public bool MoveNext()
            {
                if (_done) return false;
                
                ++_pos;
                
                var action = new ListGetAction(_pos);
                
                _owner._actions.Add(action);

                try
                {
                    action.Wait(TimeSpan.MaxValue);
                }
                catch (ArgumentOutOfRangeException)
                {
                    Current = null!;
                    _done = true;
                    return false;
                }
                
                Current = action.Value;

                _done = action.Count - 1 == _pos;

                return true;
            }

            public void Reset()
            {
                _pos = -1;
                _done = false;
            }
            
            public void Dispose() { }
        }
    }
}