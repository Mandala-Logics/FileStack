using System;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using MandalaLogics.Encoding;

namespace MandalaLogics.Splice
{
    public class SpliceHeader : IEncodable
    {
        public static readonly long EncodedSize = new SpliceHeader().Encode().WriteToMemoryStream().Length;
        
        public int BlockCount { get; internal set; }
        public byte[] TypeHash { get; }

        private SpliceHeader()
        {
            TypeHash = new byte[32];
        }
        
        internal SpliceHeader(Type type)
        {
            BlockCount = 0;

            var bytes = System.Text.Encoding.UTF8.GetBytes(type.Name);

            using var hasher = SHA256.Create();

            TypeHash = hasher.ComputeHash(bytes);
        }
        
        public SpliceHeader(DecodingHandle handle)
        {
            BlockCount = handle.Next<int>();
            TypeHash = handle.Next<byte[]>();
        }

        public bool CompareType(Type type)
        {
            var bytes = System.Text.Encoding.UTF8.GetBytes(type.Name);

            using var hasher = SHA256.Create();

            var hash = hasher.ComputeHash(bytes);

            return hash.SequenceEqual(TypeHash);
        }

        void IEncodable.DoEncode(EncodingHandle handle)
        {
            handle.Append(BlockCount);
            handle.Append(TypeHash);
        }

        public void WriteSelf(Stream stream)
        {
            stream.Seek(0L, SeekOrigin.Begin);
            this.Encode().Write(stream);
        }
    }
}