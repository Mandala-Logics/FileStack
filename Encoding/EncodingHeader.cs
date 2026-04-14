using System;

namespace MandalaLogics.Encoding
{
    public readonly struct EncodingHeader
    {
        public TypeCode TypeCode {get;}
        public EncodedValueType EncodedType {get;}
        public Type? Type { get; }

        internal EncodingHeader(TypeCode typeCode, EncodedValueType valueType, Type? type = null)
        {
            TypeCode = typeCode;
            EncodedType = valueType;
            Type = type;
        }
    }
}