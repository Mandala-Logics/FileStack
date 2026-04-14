using MandalaLogics.Encoding;

namespace MandalaLogics.Database
{
    internal readonly struct CachedEncodable
    {
        public int Size { get; }
        public IEncodable Value { get; }

        public CachedEncodable(int size, IEncodable value)
        {
            Size = size;
            Value = value;
        }
    }
}