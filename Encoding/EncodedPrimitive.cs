using System;
using System.IO;

namespace MandalaLogics.Encoding
{
    public sealed class EncodedPrimitive : EncodedValue
    {
        public static readonly EncodedPrimitive True = new EncodedPrimitive(true);
        public static readonly EncodedPrimitive False = new EncodedPrimitive(false);
        
        public override object Value => _value;
        public override TypeCode TypeCode => _value.GetTypeCode();
        public override EncodedValueType EncodedType => EncodedValueType.Primitive;
        public override bool IsFixedSize => TypeCode != TypeCode.String;

        private readonly IConvertible _value;

        public EncodedPrimitive(IConvertible value)
        {
            _value = value;

            if (TypeCode == TypeCode.DBNull || TypeCode == TypeCode.Object || TypeCode == TypeCode.Empty) { throw new NotSupportedException("DBNull type, empty type and object type are not supported."); }
        }

        public override ulong GetLongHash()
        {
            return LongHashPrimitive(_value);
        }

        internal override void WriteBytes(BinaryWriter bw)
        {
            switch (TypeCode)
            {
                case TypeCode.Boolean:
                    bw.Write((bool)_value);
                    break;
                case TypeCode.Byte:
                    bw.Write((byte)_value);
                    break;
                case TypeCode.Char:
                    bw.Write((char)_value);
                    break;
                case TypeCode.DateTime:
                    bw.Write(((DateTime)_value).ToBinary());
                    break;
                case TypeCode.Decimal:
                    bw.Write((decimal)_value);
                    break;
                case TypeCode.Double:
                    bw.Write((double)_value);
                    break;
                case TypeCode.Int16:
                    bw.Write((short)_value);
                    break;
                case TypeCode.Int32:
                    bw.Write((int)_value);
                    break;
                case TypeCode.Int64:
                    bw.Write((long)_value);
                    break;
                case TypeCode.SByte:
                    bw.Write((sbyte)_value);
                    break;
                case TypeCode.Single:
                    bw.Write((float)_value);
                    break;
                case TypeCode.String:
                    bw.Write((string)_value);
                    break;
                case TypeCode.UInt16:
                    bw.Write((ushort)_value);
                    break;
                case TypeCode.UInt32:
                    bw.Write((uint)_value);
                    break;
                case TypeCode.UInt64:
                    bw.Write((ulong)_value);
                    break;
                default:
                    throw new ProgrammerException("Invalid type code.");
            }
        }

        internal static EncodedPrimitive ReadPrimitive(TypeCode type, BinaryReader br)
        {
            switch (type)
            {
                case TypeCode.Boolean:
                    return new EncodedPrimitive(br.ReadBoolean());
                case TypeCode.Byte:
                    return new EncodedPrimitive(br.ReadByte());
                case TypeCode.Char:
                    return new EncodedPrimitive(br.ReadChar());
                case TypeCode.DateTime:
                    return new EncodedPrimitive(DateTime.FromBinary(br.ReadInt64()));
                case TypeCode.Decimal:
                    return new EncodedPrimitive(br.ReadDecimal());
                case TypeCode.Double:
                    return new EncodedPrimitive(br.ReadDouble());
                case TypeCode.Int16:
                    return new EncodedPrimitive(br.ReadInt16());
                case TypeCode.Int32:
                    return new EncodedPrimitive(br.ReadInt32());
                case TypeCode.Int64:
                    return new EncodedPrimitive(br.ReadInt64());
                case TypeCode.SByte:
                    return new EncodedPrimitive(br.ReadSByte());
                case TypeCode.Single:
                    return new EncodedPrimitive(br.ReadSingle());
                case TypeCode.String:
                    return new EncodedPrimitive(br.ReadString());
                case TypeCode.UInt16:
                    return new EncodedPrimitive(br.ReadUInt16());
                case TypeCode.UInt32:
                    return new EncodedPrimitive(br.ReadUInt32());
                case TypeCode.UInt64:
                    return new EncodedPrimitive(br.ReadUInt64());
                default:
                    throw new PlaceholderException();
            }
        }

        public static ulong LongHashPrimitive(IConvertible obj)
        {
            switch (obj.GetTypeCode())
            {
                case TypeCode.Boolean:
                    return (bool)obj ? ulong.MaxValue : 0UL;

                case TypeCode.Byte:
                case TypeCode.Char:
                case TypeCode.UInt16:
                case TypeCode.UInt32:
                case TypeCode.UInt64:
                case TypeCode.Int16:
                case TypeCode.Int32:
                case TypeCode.Int64:
                case TypeCode.SByte:
                    return obj.ToUInt64(null);

                case TypeCode.DateTime:
                    return (ulong)((DateTime)obj).ToBinary();

                case TypeCode.Decimal:
                {
                    var bits = decimal.GetBits((decimal)obj);
                    
                    return ((ulong)(uint)bits[0])
                         ^ ((ulong)(uint)bits[1] << 16)
                         ^ ((ulong)(uint)bits[2] << 32)
                         ^ ((ulong)(uint)bits[3] << 48);
                }

                case TypeCode.Double:
                    return (ulong)BitConverter.DoubleToInt64Bits((double)obj);
                
                case TypeCode.Single:
                    return (ulong)BitConverter.SingleToInt32Bits((float)obj);

                case TypeCode.String:
                {
                    const ulong offset = 14695981039346656037UL;
                    const ulong prime  = 1099511628211UL;

                    var hash = offset;
                    
                    foreach (var c in (string)obj)
                    {
                        hash ^= c;
                        hash *= prime;
                    }
                    return hash;
                }
                
                default:
                    throw new ProgrammerException("Invalid type code.");
            }
        }
    }
}