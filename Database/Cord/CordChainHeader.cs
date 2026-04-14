using System.Collections;
using System.Collections.Generic;
using System.Linq;
using MandalaLogics.Encoding;

namespace MandalaLogics.Database
{
    [Encodable("crd_cha_hdr")]
    internal sealed class CordChainHeader : CordBlockHeader, IReadOnlyList<int>
    {
        private readonly List<int> _blocks;
        
        public int Count => _blocks.Count;
        public int this[int index] => _blocks[index];
        public ulong KeyHash { get; }
        public ulong ValueHash { get; }
        
        internal CordChainHeader(ulong keyHash, ulong valueHash)
        {
            _blocks = new List<int>();
            KeyHash = keyHash;
            ValueHash = valueHash;
        }

        public CordChainHeader(DecodingHandle handle) : base(handle)
        {
            _blocks = handle.Next<int[]>().ToList();
            KeyHash = handle.Next<ulong>();
            ValueHash = handle.Next<ulong>();
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
            handle.Append(KeyHash);
            handle.Append(ValueHash);
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