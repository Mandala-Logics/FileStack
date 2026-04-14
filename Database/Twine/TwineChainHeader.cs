using System.Collections;
using System.Collections.Generic;
using System.Linq;
using MandalaLogics.Encoding;

namespace MandalaLogics.Database
{
    public sealed partial class Twine<T>
    {
        [Encodable("twi_cha_hdr")]
        internal class TwineChainHeader : TwineBlockHeader, IReadOnlyList<int>
        {
            private readonly List<int> _blocks;

            public int Count => _blocks.Count;
            public int this[int index] => _blocks[index];
            public ulong Hash { get; }

            internal TwineChainHeader(ulong hash)
            {
                _blocks = new List<int>();
                Hash = hash;
            }

            public TwineChainHeader(DecodingHandle handle) : base(handle)
            {
                _blocks = handle.Next<int[]>().ToList();
                Hash = handle.Next<ulong>();
            }

            internal void Append(int index)
            {
                _blocks.Add(index);
            }

            internal void Truncate(int amount)
            {
                _blocks.RemoveRange(_blocks.Count - amount, amount);
            }

            public override void DoEncode(EncodingHandle handle)
            {
                base.DoEncode(handle);

                handle.Append(_blocks);
                handle.Append(Hash);
            }

            public IEnumerator<int> GetEnumerator()
            {
                return _blocks.GetEnumerator();
            }

            IEnumerator IEnumerable.GetEnumerator()
            {
                return ((IEnumerable)_blocks).GetEnumerator();
            }
        }
    }
}