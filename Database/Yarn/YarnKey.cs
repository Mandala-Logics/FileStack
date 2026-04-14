using System;
using MandalaLogics.Encoding;

namespace MandalaLogics.Database
{
    [Encodable("yrn_key")]
    public class YarnKey : IEncodable, IEquatable<YarnKey>
    {
        public IConvertible Key { get; }

        public YarnKey(IConvertible key)
        {
            Key = key;
        }

        public YarnKey(DecodingHandle handle)
        {
            Key = handle.Next<IConvertible>();
        }
        
        public void DoEncode(EncodingHandle handle)
        {
            handle.Append(Key);
        }
        
        public bool Equals(YarnKey other)
        {
            return other.Key.Equals(Key);
        }

        public override bool Equals(object? obj)
        {
            return obj is YarnKey yk && Equals(yk);
        }

        public override int GetHashCode()
        {
            return HashCode.Combine(Key);
        }
    }
}