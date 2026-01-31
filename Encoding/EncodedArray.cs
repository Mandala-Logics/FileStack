using System;
using System.Collections;
using System.IO;
using System.Runtime.InteropServices;

namespace MandalaLogics.Encoding
{
    public sealed class EncodedArray : EncodedValue
    {
        public override object Value
        {
            get
            {
                if (arr is Array) { return arr; }
                else if (iEnum is IEnumerable) { return iEnum; }
                else { throw new PlaceholderException(); }
            }
        }
        public override TypeCode TypeCode => elementType;
        public override EncodedValueType EncodedType => EncodedValueType.Array;
        public int Count
        {
            get
            {
                if (arr is Array) { return arr.Length; }
                else if (iEnum is IEnumerable)
                {
                    int n = 0;

                    foreach(var x in iEnum) { n++; }

                    return n;
                }
                else { throw new PlaceholderException(); }
            }
        }

        private readonly Array? arr;
        private readonly IEnumerable? iEnum;
        private readonly TypeCode elementType; 

        private EncodedArray(IEnumerable val, TypeCode elementType)
        {
            iEnum = val;
            this.elementType = elementType;
        }

        private EncodedArray(Array val, TypeCode elementType)
        {
            arr = val;
            this.elementType = elementType;
        }

        public static EncodedArray Create(Array val)
        {
            if (val.Rank != 1) { throw new NotSupportedException("Only 1D arrays supported."); }

            var elementType = val.GetType().GetElementType();

            if (ConvertableType.IsAssignableFrom(elementType)) //array of primitives
            {
                var tc = Type.GetTypeCode(elementType);

                if (tc == TypeCode.DBNull || tc == TypeCode.Empty) { throw new NotSupportedException("DBNull type and empty type are not supported."); }

                return new EncodedArray(val, tc);
            }
            else if (EncodedObject.EncodingType.IsAssignableFrom(elementType)) //array of IEncodable
            {
                return new EncodedArray(val, TypeCode.Object);
            }
            else
            {
                throw new NotSupportedException($"Array of {elementType.Name} is not supported.");
            }
        }

        public static EncodedArray Create(IEnumerable val)
        {
            var type = val.GetType();

            if (type.IsGenericType) //generic collection, e.g. List<T>
            {
                var genericTypes = type.GenericTypeArguments;

                if (genericTypes.Length > 1) { throw new NotSupportedException("This type is not supported."); }

                var elementType = genericTypes[0];

                var tc = Type.GetTypeCode(elementType);

                if (tc == TypeCode.DBNull || tc == TypeCode.Empty) { throw new NotSupportedException("DBNull type and empty type are not supported."); }
                else if (tc == TypeCode.Object && EncodedObject.EncodingType.IsAssignableFrom(elementType)) { throw new NotSupportedException("A list of objects, other than iEncodable, is not supported."); } 

                return new EncodedArray(val, tc);
            }
            else
            {
                throw new NotSupportedException($"Type {type.Name} is not supported.");
            }
        }

        internal override void WriteBytes(BinaryWriter bw)
        {
            if (arr is Array)
            {
                bw.Write(arr.Length);

                if (elementType == TypeCode.Object)
                {
                    foreach (var o in arr)
                    {
                        var ie = (IEncodable)o;

                        var eo = EncodedObject.Create(ie);

                        eo.WriteBytes(bw);
                    }
                }
                else if (elementType == TypeCode.String)
                {
                    foreach (var s in (string[])arr)
                    {
                        bw.Write(s);
                    }
                }
                else if (elementType == TypeCode.Decimal)
                {
                    foreach (decimal d in (decimal[])arr) bw.Write(d);
                }
                else if (elementType == TypeCode.DateTime)
                {
                    foreach (DateTime dt in (DateTime[])arr) bw.Write(dt.ToBinary());
                }
                else //array of primitives
                {
                    unsafe
                    {
                        var h = GCHandle.Alloc(arr, GCHandleType.Pinned);

                        int len = Buffer.ByteLength(arr);

                        Span<byte> b;

                        try
                        {
                            void* p = h.AddrOfPinnedObject().ToPointer();

                            b = new Span<byte>(p, len);

                            bw.BaseStream.Write(b);
                        }
                        finally
                        {
                            h.Free();
                        }
                    }
                }
            }
            else if (iEnum is IEnumerable)
            {
                bw.Write(Count);

                foreach (var o in iEnum)
                {
                    switch (elementType)
                    {
                        case TypeCode.Boolean:
                            bw.Write((bool)o);
                            break;
                        case TypeCode.Byte:
                            bw.Write((byte)o);
                            break;
                        case TypeCode.Char:
                            bw.Write((char)o);
                            break;
                        case TypeCode.DateTime:
                            bw.Write(((DateTime)o).ToBinary());
                            break;
                        case TypeCode.Decimal:
                            bw.Write((decimal)o);
                            break;
                        case TypeCode.Double:
                            bw.Write((double)o);
                            break;
                        case TypeCode.Int16:
                            bw.Write((short)o);
                            break;
                        case TypeCode.Int32:
                            bw.Write((int)o);
                            break;
                        case TypeCode.Int64:
                            bw.Write((long)o);
                            break;
                        case TypeCode.Object:
                            
                            var ie = (IEncodable)o;

                            var eo = EncodedObject.Create(ie);

                            eo.WriteBytes(bw);

                            break;

                        case TypeCode.SByte:
                            bw.Write((sbyte)o);
                            break;
                        case TypeCode.Single:
                            bw.Write((float)o);
                            break;
                        case TypeCode.String:
                            bw.Write((string)o);
                            break;
                        case TypeCode.UInt16:
                            bw.Write((ushort)o);
                            break;
                        case TypeCode.UInt32:
                            bw.Write((uint)o);
                            break;
                        case TypeCode.UInt64:
                            bw.Write((ulong)o);
                            break;
                    }
                }
            }
        }

        internal static EncodedArray ReadArray(TypeCode elementType, BinaryReader br)
        {
            var len = br.ReadInt32();

            var t = GetType(elementType);

            var arr = Array.CreateInstance(GetType(elementType), len);

            if (t.IsPrimitive)
            {
                var byteLen = Buffer.ByteLength(arr);

                unsafe
                {
                    var h = GCHandle.Alloc(arr, GCHandleType.Pinned);

                    Span<byte> b;

                    try
                    {
                        void* p = h.AddrOfPinnedObject().ToPointer();

                        b = new Span<byte>(p, byteLen);

                        br.BaseStream.Read(b);
                    }
                    finally
                    {
                        h.Free();
                    }
                }
            }
            else
            {
                for (int x = 0; x < len; x++)
                {
                    object o = elementType switch
                    {
                        TypeCode.DateTime => new DateTime(br.ReadInt64()),
                        TypeCode.Decimal => br.ReadDecimal(),
                        TypeCode.Object => (IEncodable)EncodedObject.ReadOject(br).Value,
                        TypeCode.String => br.ReadString(),
                        _ => throw new PlaceholderException(),
                    };

                    arr.SetValue(o, x);
                }
            }

            return new EncodedArray(arr, elementType);
        }

        internal static Type GetType(TypeCode tc)
        {
            return tc switch
            {
                TypeCode.Boolean => typeof(bool),
                TypeCode.Byte => typeof(byte),
                TypeCode.Char => typeof(char),
                TypeCode.DateTime => typeof(DateTime),
                TypeCode.Decimal => typeof(decimal),
                TypeCode.Double => typeof(double),
                TypeCode.Int16 => typeof(short),
                TypeCode.Int32 => typeof(int),
                TypeCode.Int64 => typeof(long),
                TypeCode.Object => typeof(IEncodable),
                TypeCode.SByte => typeof(sbyte),
                TypeCode.Single => typeof(float),
                TypeCode.String => typeof(string),
                TypeCode.UInt16 => typeof(ushort),
                TypeCode.UInt32 => typeof(uint),
                TypeCode.UInt64 => typeof(ulong),
                _ => throw new PlaceholderException(),
            };
        }
    }
}