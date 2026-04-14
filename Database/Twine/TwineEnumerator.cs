using System.Collections;
using System.Collections.Generic;

namespace MandalaLogics.Database
{
    public sealed partial class Twine<T>
    {
        internal class TwineEnumerator : IEnumerator<T>
        {
            public T Current { get; private set; } = null!;
            object? IEnumerator.Current => Current;

            private readonly Twine<T> _owner;
            private int _bucket;
            private IEnumerator<TwineEntry>? _e;

            public TwineEnumerator(Twine<T> owner)
            {
                _owner = owner;
                
                Reset();
            }
            
            public bool MoveNext()
            {
                _owner._bucketLock.Wait();

                try
                {
                    if (_e is null) return false;

                    if (_e.MoveNext())
                    {
                        Current = _e.Current!.Get();
                        return true;
                    }

                    do
                    {
                        var i = _bucket;

                        for (var x = _bucket + 1; x < (int)BucketCount; x++)
                        {
                            if (_owner._buckets[x] is null) continue;

                            _bucket = x;
                            break;
                        }

                        if (_bucket == i)
                        {
                            _e = null;
                            return false;
                        }

                        _e = _owner._buckets[_bucket]!.GetEnumerator();
                        
                    } while (!_e.MoveNext()); //list is empty
                    
                    Current = _e.Current!.Get();
                    return true;
                }
                finally
                {
                    _owner._bucketLock.Release();
                }
            }

            public void Reset()
            {
                _owner._bucketLock.Wait();
                
                try
                {
                    _bucket = -1;
                    _e = null;
                
                    for (var x = 0; x < (int)BucketCount; x++)
                    {
                        if (_owner._buckets[x] is null) continue;

                        _bucket = x;
                        break;
                    }

                    if (_bucket == -1) return;

                    _e = _owner._buckets[_bucket]?.GetEnumerator();
                }
                finally
                {
                    _owner._bucketLock.Release();
                }
            }
            
            public void Dispose() { }
        }
    }
}