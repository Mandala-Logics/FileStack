using MandalaLogics.Encoding;

namespace MandalaLogics.Database
{
    [Encodable("spl_blk_hdr")]
    public class SpliceBlockHeader : IEncodable
    {
        public static readonly int EncodedSize = (int)new SpliceBlockHeader().Encode().WriteToMemoryStream().Length;
        
        public bool Disused { get; internal set; }
        public int BlockIndex { get; private set; } = -1;

        internal SpliceBlockHeader()
        {
            Disused = true;
        }

        internal void SetIndex(int id)
        {
            BlockIndex = id;
        }

        public SpliceBlockHeader(DecodingHandle handle)
        {
            Disused = handle.Next<bool>();
        }
        
        public virtual void DoEncode(EncodingHandle handle)
        {
            handle.Append(Disused);
        }
    }
}