using System.IO;
using MandalaLogics.Encoding;

namespace MandalaLogics.Packing
{
    [Encodable("rbn_hdr")]
    internal class RibbonHeader : IEncodable
    {
        public int KnotCount { get; internal set; }
        public uint LastStrandId { get; internal set; }

        public RibbonHeader()
        {
            KnotCount = 0;
            LastStrandId = 0U;
        }
        
        public RibbonHeader(DecodingHandle handle)
        {
            KnotCount = handle.Next<int>();
            LastStrandId = handle.Next<uint>();
        }

        void IEncodable.DoEncode(EncodingHandle handle)
        {
            handle.Append(KnotCount);
            handle.Append(LastStrandId);
        }

        public void WriteSelf(Stream stream)
        {
            stream.Seek(0L, SeekOrigin.Begin);
            this.Encode().Write(stream);
        }
        
    }
}