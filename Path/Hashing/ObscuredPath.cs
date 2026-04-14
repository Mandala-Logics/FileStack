using System;
using System.Linq;
using System.Security.Cryptography;
using MandalaLogics.Encoding;

namespace MandalaLogics.Path.Hashing
{
    [Encodable("obs_path")]
    public sealed class ObscuredPath : IEncodable, IEquatable<ObscuredPath>
    {
        private const int Iterations = 200_000;
        private const int HashSize = 32;
        private const int SaltLength = 16;
        
        private readonly byte[] _salt;
        private readonly byte[] _string;
        private readonly int _count;
        private readonly int _finalLength;
        
        public ObscuredPath(DecodingHandle handle)
        {
            _salt = handle.Next<byte[]>();
            _string = handle.Next<byte[]>();
            _count = handle.Next<int>();
            _finalLength = handle.Next<int>();
        }

        public ObscuredPath(PathBase path)
        {
            _count = path.Count;

            _finalLength = path.Count > 0 ? path[^1].Length : -1;
                
            var arr = System.Text.Encoding.UTF8.GetBytes(path.Path);

            _salt = new byte[SaltLength];
            
            RandomNumberGenerator.Fill(_salt);

            var deriver = new Rfc2898DeriveBytes(arr, _salt, Iterations);

            _string = deriver.GetBytes(HashSize);
        }
        
        void IEncodable.DoEncode(EncodingHandle handle)
        {
            handle.Append(_salt);
            handle.Append(_string);
            handle.Append(_count);
            handle.Append(_finalLength);
        }
        
        public bool Guess(PathBase path)
        {
            if (path.Count != _count) return false;
            
            var len = path.Count > 0 ? path[^1].Length : -1;

            if (len != _finalLength) return false;
            
            var arr = System.Text.Encoding.UTF8.GetBytes(path.Path);
            
            var deriver = new Rfc2898DeriveBytes(arr, _salt, Iterations);

            var derived = deriver.GetBytes(HashSize);

            return CryptographicOperations.FixedTimeEquals(_string, derived);
        }

        public bool Equals(ObscuredPath other)
        {
            return _finalLength == other._finalLength && _count == other._count && 
                _salt.SequenceEqual(other._salt) && _string.SequenceEqual(other._string);
        }

        public override bool Equals(object? obj)
        {
            return obj is ObscuredPath other && Equals(other);
        }

        public override int GetHashCode()
        {
            return HashCode.Combine(_salt, _string);
        }
    }
}