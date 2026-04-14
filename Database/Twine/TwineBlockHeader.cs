using MandalaLogics.Encoding;

namespace MandalaLogics.Database
{
    public sealed partial class Twine<T>
    {
        [Encodable("twi_blk_hdr")]
        internal class TwineBlockHeader : IEncodable
        {
            public static readonly int EncodedSize = (int)new TwineBlockHeader().Encode().WriteToMemoryStream().Length;

            public bool Disused { get; internal set; }
            public int BlockIndex { get; private set; } = -1;

            internal TwineBlockHeader()
            {
                Disused = true;
            }

            internal void SetIndex(int id)
            {
                BlockIndex = id;
            }

            public TwineBlockHeader(DecodingHandle handle)
            {
                Disused = handle.Next<bool>();
            }

            public virtual void DoEncode(EncodingHandle handle)
            {
                handle.Append(Disused);
            }
        }
    }
}