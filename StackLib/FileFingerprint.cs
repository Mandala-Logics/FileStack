using System;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using MandalaLogics.Encoding;

namespace MandalaLogics.Stacking
{
    public readonly struct FileFingerprint : IEncodable, IEquatable<FileFingerprint>
    {
        public long Length { get; }

        private readonly ulong[] _data;

        public FileFingerprint(DecodingHandle handle)
        {
            Length = handle.Next<long>();
            _data = handle.Next<ulong[]>();
        }

        public FileFingerprint(long length)
        {
            Length = length;
            _data = new ulong[4];
        }

        public FileFingerprint(Stream stream)
        {
            stream.Seek(0L, SeekOrigin.Begin);
            
            Length = stream.Length;

            var hasher = SHA256.Create();

            var hash = hasher.ComputeHash(stream);

            _data = new ulong[4];
            
            Buffer.BlockCopy(hash, 0, _data, 0, hash.Length);
            
            stream.Seek(0L, SeekOrigin.Begin);
        }

        void IEncodable.DoEncode(EncodingHandle handle)
        {
            handle.Append(Length);
            handle.Append(_data);
        }

        public bool Equals(FileFingerprint other)
        {
            return Length == other.Length && _data.SequenceEqual(other._data);
        }

        public override bool Equals(object? obj)
        {
            return obj is FileFingerprint other && Equals(other);
        }

        public override int GetHashCode()
        {
            return HashCode.Combine(_data, Length);
        }
    }
}