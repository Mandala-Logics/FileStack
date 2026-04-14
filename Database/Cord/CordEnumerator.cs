using System.Collections;
using System.Collections.Generic;

namespace MandalaLogics.Database
{
    public sealed partial class Cord<TKey, TValue>
    {
        internal class CordEnumerator : IEnumerator<KeyValuePair<TKey, TValue>>
        {
            public KeyValuePair<TKey, TValue> Current { get; private set; } = default;
            object? IEnumerator.Current => Current;

            private readonly Cord<TKey, TValue> _owner;
            private int _bucket;
            private IEnumerator<CordEntry>? _e;

            public CordEnumerator(Cord<TKey, TValue> owner)
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
                        Current = new KeyValuePair<TKey, TValue>(_e.Current!.GetKey(), _e.Current.GetValue());
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
                    
                    Current = new KeyValuePair<TKey, TValue>(_e.Current!.GetKey(), _e.Current.GetValue());
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