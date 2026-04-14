using MandalaLogics.Encoding;

namespace MandalaLogics.Database
{
    [Encodable("crd_blk_hdr")]
    internal class CordBlockHeader : IEncodable
    {
        public static readonly int EncodedSize = (int)new CordBlockHeader().Encode().WriteToMemoryStream().Length;
        
        public bool Disused { get; internal set; }
        public int BlockIndex { get; private set; } = -1;

        internal CordBlockHeader()
        {
            Disused = true;
        }

        internal void SetIndex(int id)
        {
            BlockIndex = id;
        }

        public CordBlockHeader(DecodingHandle handle)
        {
            Disused = handle.Next<bool>();
        }
        
        public virtual void DoEncode(EncodingHandle handle)
        {
            handle.Append(Disused);
        }
    }
}