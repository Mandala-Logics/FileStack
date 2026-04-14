using System;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using MandalaLogics.Encoding;

namespace MandalaLogics.Database
{
    [Encodable("crd_hdr")]
    public class CordHeader : IEncodable
    {
        public int BlockCount { get; internal set; }
        public int ChainCount { get; internal set; }
        public string KeyTypeKey { get; }
        public string ValueTypeKey { get; }
        
        internal CordHeader(Type keyType, Type valueType)
        {
            BlockCount = 0;
            ChainCount = 0;

            var keyKey = EncodingRegister.GetKey(keyType);

            if (keyKey is null)
                throw new EncodingException($"Type {keyType.FullName} is not registered.");
            
            var valKey = EncodingRegister.GetKey(valueType);

            if (valKey is null)
                throw new EncodingException($"Type {valueType.FullName} is not registered.");

            KeyTypeKey = keyKey;
            ValueTypeKey = valKey;
        }
        
        public CordHeader(DecodingHandle handle)
        {
            BlockCount = handle.Next<int>();
            ChainCount = handle.Next<int>();
            KeyTypeKey = handle.Next<string>();
            ValueTypeKey = handle.Next<string>();
        }

        public void CompareKeyType(Type type)
        {
            var key = EncodingRegister.GetKey(type);

            if (key != KeyTypeKey)
                throw new TypeMismatchException(type, KeyTypeKey);
        }
        
        public void CompareValueType(Type type)
        {
            var key = EncodingRegister.GetKey(type);

            if (key != ValueTypeKey)
                throw new TypeMismatchException(type, ValueTypeKey);
        }

        void IEncodable.DoEncode(EncodingHandle handle)
        {
            handle.Append(BlockCount);
            handle.Append(ChainCount);
            handle.Append(KeyTypeKey);
            handle.Append(ValueTypeKey);
        }

        public void WriteSelf(Stream stream)
        {
            stream.Seek(0L, SeekOrigin.Begin);
            this.Encode().Write(stream);
        }
    }
}