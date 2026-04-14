using System;
using System.IO;
using MandalaLogics.Encoding;

namespace MandalaLogics.Database
{
    [Encodable("spl_hdr")]
    public class SpliceHeader : IEncodable
    {
        public int BlockCount { get; internal set; }
        public string TypeKey { get; }
        
        internal SpliceHeader(Type type)
        {
            BlockCount = 0;
            
            var key = EncodingRegister.GetKey(type);

            if (key is null)
                throw new EncodingException($"Type {type.FullName} is not registered.");

            TypeKey = key;
        }
        
        public SpliceHeader(DecodingHandle handle)
        {
            BlockCount = handle.Next<int>();
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
            handle.Append(TypeKey);
        }

        public void WriteSelf(Stream stream)
        {
            stream.Seek(0L, SeekOrigin.Begin);
            this.Encode().Write(stream);
        }
    }
}