using System;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using MandalaLogics.Encoding;

namespace MandalaLogics.Path.Hashing
{
    [Encodable("file_fing")]
    public sealed class FileFingerprint : IEncodable, IEquatable<FileFingerprint>
    {
        private readonly long _length;
        private readonly ulong[] _data;

        public FileFingerprint(DecodingHandle handle)
        {
            _length = handle.Next<long>();
            _data = handle.Next<ulong[]>();
        }

        public FileFingerprint(long length)
        {
            _length = length;
            _data = new ulong[4];
        }

        public FileFingerprint(Stream stream)
        {
            stream.Seek(0L, SeekOrigin.Begin);
            
            _length = stream.Length;

            var hasher = SHA256.Create();

            var hash = hasher.ComputeHash(stream);

            _data = new ulong[4];
            
            Buffer.BlockCopy(hash, 0, _data, 0, hash.Length);
            
            stream.Seek(0L, SeekOrigin.Begin);
        }

        void IEncodable.DoEncode(EncodingHandle handle)
        {
            handle.Append(_length);
            handle.Append(_data);
        }

        public bool Equals(FileFingerprint other)
        {
            return _length == other._length && _data.SequenceEqual(other._data);
        }

        public override bool Equals(object? obj)
        {
            return obj is FileFingerprint other && Equals(other);
        }

        public override int GetHashCode()
        {
            return HashCode.Combine(_data, _length);
        }
    }
}