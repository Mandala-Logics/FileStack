using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;

namespace MandalaLogics.Encoding
{
    public sealed class EncodedObject : EncodedValue
    {
        public override object Value {get;}
        public override TypeCode TypeCode => TypeCode.Object;
        public override EncodedValueType EncodedType => EncodedValueType.Object;

        public override bool IsFixedSize
        {
            get
            {
                if (_stack is null)
                {
                    var handle = new EncodingHandle();

                    ((IEncodable)Value).DoEncode(handle);

                    _stack = handle.stack;
                }

                foreach (var ev in _stack)
                {
                    if (!ev.IsFixedSize) return false;
                }

                return true;
            }
        }

        private Stack<EncodedValue>? _stack;

        private EncodedObject(IEncodable obj, Stack<EncodedValue> stack)
        {
            _stack = stack;
            Value = obj;
        }

        private EncodedObject(IEncodable obj)
        {
            Value = obj;
            _stack = null;
        }

        internal override void WriteBytes(BinaryWriter bw)
        {
            if (_stack is null)
            {
                var handle = new EncodingHandle();

                ((IEncodable)Value).DoEncode(handle);

                _stack = handle.stack;
            }

            var type = Value.GetType();

            if (!EncodingRegister.IsRegistered(type)) { throw new EncodingException($"Type ({type.FullName}) is not registered."); }

            bw.Write(EncodingRegister.GetKey(type)!);
            bw.Write(_stack.Count);
            
            while (_stack.TryPop(out EncodedValue ev))
            {
                bw.Write((byte)ev.EncodedType);
                bw.Write((int)ev.TypeCode);
                ev.WriteBytes(bw);
            }
        }
        
        public override ulong GetLongHash()
        {
            if (_stack is null)
            {
                var handle = new EncodingHandle();

                ((IEncodable)Value).DoEncode(handle);

                _stack = handle.stack;
            }
            
            var hashCode = new LongHashCode();

            foreach (var ev in _stack)
            {
                hashCode.Add(ev.GetLongHash());
            }

            return hashCode.ToUInt64();
        }

        public static EncodedObject Create(IEncodable encodable)
        {
            var handle = new EncodingHandle();

            encodable.DoEncode(handle);

            return new EncodedObject(encodable, handle.stack);
        }

        internal static EncodedObject ReadObject(BinaryReader br)
        {
            var name = br.ReadString();

            var type = EncodingRegister.GetType(name);

            if (type is null) { throw new EncodingException($"The type name read ({name}) was not registered."); }

            var len = br.ReadInt32();

            var stack = new Stack<EncodedValue>(len);

            for (int x = 0; x < len; x++)
            {
                var et = ReadEncodedType(br.BaseStream);
                var tc = ReadTypeCode(br.BaseStream);

                switch (et)
                {
                    case EncodedValueType.Primitive:
                        stack.Push(EncodedPrimitive.ReadPrimitive(tc, br));
                        break;
                    case EncodedValueType.Array:
                        stack.Push(EncodedArray.ReadArray(tc, br));
                        break;
                    case EncodedValueType.Object:
                        stack.Push(ReadObject(br));
                        break;
                }
            }

            var cons = type.GetConstructor(EncodingRegister.ConstructorType) ?? throw new EncodingException("Registered types must have a constructor which accepts a DecodingHandle as an argument.");

            var dh = new DecodingHandle(stack);

            var ret = (IEncodable)cons.Invoke(new object[] { dh });

            return new EncodedObject(ret);
        }
    }
}