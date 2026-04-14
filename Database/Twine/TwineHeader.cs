using System;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using MandalaLogics.Encoding;

namespace MandalaLogics.Database
{
    public sealed partial class Twine<T>
    {
        [Encodable("twi_hdr")]
        public class TwineHeader : IEncodable
        {
            public int BlockCount { get; internal set; }
            public int ChainCount { get; internal set; }
            public string TypeKey { get; }

            internal TwineHeader(Type type)
            {
                BlockCount = 0;
                ChainCount = 0;

                var key = EncodingRegister.GetKey(type);

                if (key is null)
                    throw new EncodingException($"Type {type.FullName} is not registered.");

                TypeKey = key;
            }

            public TwineHeader(DecodingHandle handle)
            {
                BlockCount = handle.Next<int>();
                ChainCount = handle.Next<int>();
                TypeKey = handle.Next<string>();
            }

            public void CompareType(Type type)
            {
                var key = EncodingRegister.GetKey(type);

                if (key != TypeKey)
                    throw new TypeMismatchException(type, TypeKey);
            }

            void IEncodable.DoEncode(EncodingHandle handle)
            {
                handle.Append(BlockCount);
                handle.Append(ChainCount);
                handle.Append(TypeKey);
            }

            public void WriteSelf(Stream stream)
            {
                stream.Seek(0L, SeekOrigin.Begin);
                this.Encode().Write(stream);
            }
        }
    }
}