using MandalaLogics.Encoding;

namespace MandalaLogics.Splice
{
    public class BlockHeader : IEncodable
    {
        public static readonly int EncodedSize = (int)new BlockHeader().Encode().WriteToMemoryStream().Length;
        
        public bool Disused { get; internal set; }
        public int BlockIndex { get; private set; } = -1;

        internal BlockHeader()
        {
            Disused = true;
        }

        internal void SetIndex(int id)
        {
            BlockIndex = id;
        }

        public BlockHeader(DecodingHandle handle)
        {
            Disused = handle.Next<bool>();
        }
        
        public virtual void DoEncode(EncodingHandle handle)
        {
            handle.Append(Disused);
        }
    }
}