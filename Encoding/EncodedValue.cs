using System;
using System.IO;
using System.Net.Sockets;

namespace MandalaLogics.Encoding
{
    public enum EncodedValueType : byte { Primitive = 12, Array, Object } 

    public abstract class EncodedValue
    {
        public static readonly Type ConvertableType = typeof(IConvertible);

        public const int MaxEncodedLength = 32768;

        public abstract object Value {get;}
        public abstract TypeCode TypeCode {get;}
        public abstract EncodedValueType EncodedType {get;}

        static EncodedValue()
        {
            if (!BitConverter.IsLittleEndian) { throw new NotSupportedException("Protocol assumes little-endian."); }
        }

        internal EncodedValue() { }

        public int Write(Stream stream)
        {
            using var bw = new BinaryWriter(new MemoryStream(), System.Text.Encoding.UTF8, false);

            bw.Write((byte)EncodedType);
            bw.Write((int)TypeCode);
            bw.Write(0);
        
            WriteBytes(bw);

            bw.BaseStream.Position = sizeof(int) + sizeof(byte);

            bw.Write((int)(bw.BaseStream.Length - sizeof(int) * 2 - sizeof(byte)));

            bw.BaseStream.Position = 0L;

            bw.BaseStream.CopyTo(stream);

            return (int)bw.BaseStream.Length;
        }

        public int Write(Socket socket)
        {
            using var ms = new MemoryStream();

            var bw = new BinaryWriter(ms);

            bw.Write((byte)EncodedType);
            bw.Write((int)TypeCode);
            bw.Write(0);
        
            WriteBytes(bw);

            bw.BaseStream.Position = sizeof(int) + sizeof(byte);

            bw.Write((int)(bw.BaseStream.Length - sizeof(int) * 2 - sizeof(byte)));

            bw.BaseStream.Position = 0L;

            socket.Send(ms.GetBuffer());

            return (int)ms.Length;
        }

        public MemoryStream WriteToMemoryStream()
        {
            var ms = new MemoryStream();

            var bw = new BinaryWriter(ms);

            bw.Write((byte)EncodedType);
            bw.Write((int)TypeCode);
            bw.Write(0);
        
            WriteBytes(bw);

            bw.BaseStream.Position = sizeof(int) + sizeof(byte);

            bw.Write((int)(bw.BaseStream.Length - sizeof(int) * 2 - sizeof(byte)));

            bw.BaseStream.Position = 0L;

            return ms;
        }

        internal abstract void WriteBytes(BinaryWriter bw);

        public static int Read(Socket socket, out EncodedValue value)
        {
            var et = ReadEncodedType(socket);

            var tc = ReadTypeCode(socket);

            int len = socket.ReciveInt32();

            if (len < 0 || len > MaxEncodedLength) { throw new EncodingException("Invalid encoded information read."); }

            var buffer = new byte[len];

            int r = socket.ReceiveExactly(buffer, 0, len, TimeSpan.MaxValue);

            if (r != buffer.Length) { throw new EncodingException("Failed to read enough bytes from socket."); }

            using var br = new BinaryReader(new MemoryStream(buffer, false), System.Text.Encoding.UTF8, false);

            value = et switch
            {
                EncodedValueType.Primitive => EncodedPrimitive.ReadPrimitive(tc, br),
                EncodedValueType.Array => EncodedArray.ReadArray(tc, br),
                EncodedValueType.Object => EncodedObject.ReadOject(br),
                _ => throw new PlaceholderException(),
            };

            return r + sizeof(int) * 2 + sizeof(byte);
        }
        
        public static int Read(MemoryStream stream, out EncodedValue value)
        {
            var et = ReadEncodedType(stream);

            var tc = ReadTypeCode(stream);

            int len;

            try { len = stream.ReadInt32(); }
            catch (EndOfStreamException) { throw new EncodingException("End of stream."); }

            if (len < 0 || len > MaxEncodedLength) { throw new EncodingException("Invalid length read."); }

            using var br = new BinaryReader(stream, System.Text.Encoding.UTF8, false);

            value = et switch
            {
                EncodedValueType.Primitive => EncodedPrimitive.ReadPrimitive(tc, br),
                EncodedValueType.Array => EncodedArray.ReadArray(tc, br),
                EncodedValueType.Object => EncodedObject.ReadOject(br),
                _ => throw new PlaceholderException(),
            };
            return len + sizeof(int) * 2 + sizeof(byte);
        }

        public static int Read(Stream stream, out EncodedValue value)
        {
            var et = ReadEncodedType(stream);

            var tc = ReadTypeCode(stream);

            int len;

            try { len = stream.ReadInt32(); }
            catch (EndOfStreamException) { throw new EncodingException("End of stream."); }

            if (len < 0 || len > MaxEncodedLength) { throw new EncodingException("Invalid length read."); }

            var buffer = new byte[len];

            var r = stream.ReadExactly(buffer, 0, buffer.Length, TimeSpan.MaxValue);

            if (r != buffer.Length) { throw new EncodingException("Failed to read enough bytes from stream."); }

            using var br = new BinaryReader(new MemoryStream(buffer, false), System.Text.Encoding.UTF8, false);

            value = et switch
            {
                EncodedValueType.Primitive => EncodedPrimitive.ReadPrimitive(tc, br),
                EncodedValueType.Array => EncodedArray.ReadArray(tc, br),
                EncodedValueType.Object => EncodedObject.ReadOject(br),
                _ => throw new PlaceholderException(),
            };
            return r + sizeof(int) * 2 + sizeof(byte);
        }

        internal static EncodedValueType ReadEncodedType(Stream stream)
        {
            var b = stream.ReadByte();

            if (b == -1) { throw new EndOfStreamException(); }

            if (!Enum.IsDefined(typeof(EncodedValueType), (byte)b))
            {
                throw new EncodingException("Invalid encoded value type read.");
            }

            return (EncodedValueType)b;
        }

        internal static TypeCode ReadTypeCode(Stream stream)
        {
            int i = stream.ReadInt32();

            if (!Enum.IsDefined(typeof(TypeCode), i))
            {
                throw new EncodingException("Invalid encoded type read.");
            }

            return (TypeCode)i;
        }

        internal static EncodedValueType ReadEncodedType(Socket socket)
        {
            var b = socket.ReciveByte();

            if (!Enum.IsDefined(typeof(EncodedValueType), b))
            {
                throw new EncodingException("Invalid encoded value type read.");
            }

            return (EncodedValueType)b;
        }

        internal static TypeCode ReadTypeCode(Socket socket)
        {
            int i = socket.ReciveInt32();

            if (!Enum.IsDefined(typeof(TypeCode), i))
            {
                throw new EncodingException("Invalid encoded type read.");
            }

            return (TypeCode)i;
        }
    }
}