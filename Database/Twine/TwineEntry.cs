using System;
using System.IO;
using MandalaLogics.Encoding;
using MandalaLogics.Packing;

namespace MandalaLogics.Database
{
    public sealed partial class Twine<T>
    {
        internal sealed class TwineEntry
        {
            public TwineChainHeader Header { get; }
            public Twine<T> Owner { get; }
            public int CachedSize { get; private set; } = 0;
            public bool IsCached => _obj is { };

            private T? _obj;

            public TwineEntry(Twine<T> owner, TwineChainHeader header)
            {
                Owner = owner;
                Header = header;
            }

            public void TryCache(T obj, MemoryStream ms)
            {
                if (_obj is { }) return;
                
                Owner._cacheLock.Wait();

                try
                {
                    var len = (int)ms.Length;

                    if (Owner._cacheSize + len > Owner._maxCacheSize) return;

                    Owner._cacheSize += len;

                    CachedSize = len;

                    _obj = obj;
                }
                finally
                {
                    Owner._cacheLock.Release();
                }
            }

            public T Get()
            {
                if (_obj is { }) return _obj;

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
                    
                    if (ev.Value is T obj)
                    {
                        TryCache(obj, ms);

                        return obj;
                    }
                    else
                    {
                        throw new ArgumentException(
                            $"The read type was not '{typeof(T)}' as indicated in the generic argument, but was '{ev.Value.GetType()}'.");
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