using System;
using MandalaLogics.Encoding;

namespace MandalaLogics.Weave
{
    public partial class Weave
    {
        public class WeaveHandle<T> : IDisposable where T : IEncodable
        {
            internal uint StrandId { get; }
            public Weave Owner { get; }
            public T Value { get; set; }

            internal WeaveHandle(Weave owner, uint strandId, T value)
            {
                Owner = owner;
                StrandId = strandId;

                Value = value;
            }

            public void Flush()
            {
                Owner._headerLock.EnterReadLock();

                try
                {
                    if (!Owner._header.Value.Contains(StrandId)) throw new InvalidOperationException("The underlying entry has been removed from the Weave while this handle is still open.");

                    using var strand = Owner._braid.GetStrand(StrandId);

                    var r = Value.Encode().Write(strand);
                    
                    strand.SetLength(r);
                }
                finally
                {
                    Owner._headerLock.ExitReadLock();
                }
            }

            public void Dispose()
            {
                Flush();
            }
        }
    }
}