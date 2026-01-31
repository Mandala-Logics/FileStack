using System.Collections;
using System.Collections.Generic;
using System.Linq;
using MandalaLogics.Encoding;

namespace MandalaLogics.Splice
{
    public class ChainHeader : BlockHeader, IReadOnlyList<int>
    {
        private readonly List<int> _blocks;
        
        public int Count => _blocks.Count;
        public int this[int index] => _blocks[index];

        public int Ordinal { get; internal set; }
        
        internal ChainHeader()
        {
            _blocks = new List<int>();
            Ordinal = -1;
        }

        public ChainHeader(DecodingHandle handle) : base(handle)
        {
            _blocks = handle.Next<int[]>().ToList();
            Ordinal = handle.Next<int>();
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
            handle.Append(Ordinal);
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