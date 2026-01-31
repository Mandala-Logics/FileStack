using System.IO;
using MandalaLogics.Encoding;
using MandalaLogics.Streams;

namespace MandalaLogics.Packing
{
    public class BraidHeader : IEncodable
    {
        public static readonly long EncodedSize = new BraidHeader().Encode().WriteToMemoryStream().Length;
        
        public int KnotCount { get; internal set; }
        public uint LastStrandId { get; internal set; }

        internal BraidHeader()
        {
            KnotCount = 0;
            LastStrandId = 0U;
        }

        public BraidHeader(DecodingHandle handle)
        {
            KnotCount = handle.Next<int>();
            LastStrandId = handle.Next<uint>();
        }

        void IEncodable.DoEncode(EncodingHandle handle)
        {
            handle.Append(KnotCount);
            handle.Append(LastStrandId);
        }

        public void WriteSelf(StreamHandle handle)
        {
            handle.Seek(0L, SeekOrigin.Begin);
            handle.Write(this.Encode());
        }
    }
}