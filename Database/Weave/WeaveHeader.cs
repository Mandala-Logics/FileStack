using System.Collections;
using System.Collections.Generic;
using System.Linq;
using MandalaLogics.Encoding;

namespace MandalaLogics.Weave
{
    public class WeaveHeader : IEncodable, IList<uint> //strand id
    {
        private readonly List<uint> _strands;

        public int Count => _strands.Count;
        public bool IsReadOnly { get; } = false;
        
        public uint this[int index]
        {
            get => _strands[index];
            set => _strands[index] = value;
        }

        internal WeaveHeader()
        {
            _strands = new List<uint>();
        }

        public WeaveHeader(DecodingHandle handle)
        {
            _strands = handle.Next<uint[]>().ToList();
        }

        void IEncodable.DoEncode(EncodingHandle handle)
        {
            handle.Append(_strands);
        }

        public IEnumerator<uint> GetEnumerator() => _strands.GetEnumerator();

        IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();

        public void Add(uint item) => _strands.Add(item);

        public void Clear() => _strands.Clear();

        public bool Contains(uint item) => _strands.Contains(item);

        public void CopyTo(uint[] array, int arrayIndex) => _strands.CopyTo(array, arrayIndex);

        public bool Remove(uint item) => _strands.Remove(item);

        public int IndexOf(uint item) => _strands.IndexOf(item);

        public void Insert(int index, uint item) => _strands.Insert(index, item);

        public void RemoveAt(int index) => _strands.RemoveAt(index);
    }
}