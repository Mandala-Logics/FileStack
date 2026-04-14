using System;
using System.IO;
using MandalaLogics.Encoding;
using MandalaLogics.Packing;

namespace MandalaLogics.Database
{
    public sealed partial class Cord<TKey, TValue>
    {
        internal class CordEntry
        {
            public Cord<TKey, TValue> Owner { get; }
            public CordChainHeader Header { get; }
            public int CachedSize { get; private set; } = 0;
            public bool IsCached => _value is { };

            private TKey? _key;
            private TValue? _value;

            public CordEntry(Cord<TKey, TValue> owner, CordChainHeader header)
            {
                Owner = owner;
                Header = header;
            }

            public bool TryCacheValue(TValue val, int length)
            {
                if (_value is { }) return true;
                
                Owner._cacheLock.Wait();
                
                try
                {
                    if (Owner._cacheSize + length > Owner._maxCacheSize) return false;

                    _value = val;

                    Owner._cacheSize += length;

                    return true;
                }
                finally
                {
                    Owner._cacheLock.Release();
                }
            }

            public void SetKey(TKey key, int length)
            {
                if (_key is { }) return;
                
                Owner._cacheLock.Wait();
                
                try
                {
                    _key = key;

                    Owner._cacheSize += length;
                }
                finally
                {
                    Owner._cacheLock.Release();
                }
            }

            public TKey GetKey()
            {
                if (_key is { }) return _key;
                
                Owner._streamLock.Wait();

                try
                {
                    var seam = new Seam();
                    
                    foreach (var index in Header)
                    {
                        seam = seam.Append(Owner.GetStitch(index));
                    }
                    
                    using var ms = new MemoryStream(seam.Read(Owner._stream));
                    
                    EncodedValue.Read(ms, out var ev);
                    
                    if (ev.Value is TKey obj)
                    {
                        SetKey(obj, (int)ms.Length);

                        return obj;
                    }
                    else
                    {
                        throw new ArgumentException(
                            $"The read type of the key was not '{typeof(TKey)}' as indicated in the generic argument, but was '{ev.Value.GetType()}'.");
                    }
                }
                finally
                {
                    Owner._streamLock.Release();
                }
            }

            public TValue GetValue()
            {
                if (_value is { }) return _value;
                
                Owner._streamLock.Wait();

                try
                {
                    var seam = new Seam();
                    
                    foreach (var index in Header)
                    {
                        seam = seam.Append(Owner.GetStitch(index));
                    }
                    
                    using var ms = new MemoryStream(seam.Read(Owner._stream));
                    
                    //decoding the key first
                    
                    var keyLength = EncodedValue.Read(ms, out var key);
                    
                    if (key.Value is TKey k)
                    {
                        SetKey(k, keyLength);
                    }
                    else
                    {
                        throw new ArgumentException(
                            $"The read type of the key was not '{typeof(TKey)}' as indicated in the generic argument, but was '{key.Value.GetType()}'.");
                    }
                    
                    //decoding the value
                    
                    EncodedValue.Read(ms, out var val);
                    
                    if (val.Value is TValue v)
                    {
                        TryCacheValue(v, (int)ms.Length - keyLength);

                        return v;
                    }
                    else
                    {
                        throw new ArgumentException(
                            $"The read type of the value was not '{typeof(TKey)}' as indicated in the generic argument, but was '{val.Value.GetType()}'.");
                    }
                }
                finally
                {
                    Owner._streamLock.Release();
                }
            }
        }
    }
}