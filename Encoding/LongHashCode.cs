using System;

namespace MandalaLogics.Encoding
{
    internal struct LongHashCode
    {
        private ulong _hash;
        private int _count;

        public LongHashCode(ulong seed = 14695981039346656037UL)
        {
            _hash = seed;
            _count = 0;
        }

        public void Add(IConvertible value)
        {
            Add(EncodedPrimitive.LongHashPrimitive(value));
        }

        public void Add(ulong value)
        {
            unchecked
            {
                _count++;

                value = Mix(value + 0x9E3779B97F4A7C15UL + (ulong)_count);

                _hash ^= value;
                _hash = RotateLeft(_hash, 27);
                _hash *= 0x9E3779B185EBCA87UL;
                _hash ^= _hash >> 33;
            }
        }

        public void Add<T>(T value, Func<T, ulong> hasher)
        {
            if (hasher == null)
                throw new ArgumentNullException(nameof(hasher));

            Add(hasher(value));
        }

        public ulong ToUInt64()
        {
            unchecked
            {
                ulong h = _hash ^ ((ulong)_count * 0x9E3779B97F4A7C15UL);
                return Mix(h);
            }
        }

        public override string ToString() => ToUInt64().ToString();

        private static ulong RotateLeft(ulong value, int offset)
            => (value << offset) | (value >> (64 - offset));

        private static ulong Mix(ulong x)
        {
            unchecked
            {
                x ^= x >> 30;
                x *= 0xBF58476D1CE4E5B9UL;
                x ^= x >> 27;
                x *= 0x94D049BB133111EBUL;
                x ^= x >> 31;
                return x;
            }
        }
    }
}