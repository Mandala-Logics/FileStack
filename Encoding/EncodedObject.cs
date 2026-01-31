using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;

namespace MandalaLogics.Encoding
{
    public sealed class EncodedObject : EncodedValue
    {
        private static readonly Dictionary<string, Type> TypeRegister = new Dictionary<string, Type>();
        public static readonly Type[] ConstructorType = new Type[] { typeof(DecodingHandle) };
        public static readonly Type EncodingType = typeof(IEncodable);

        public override object Value {get;}
        public override TypeCode TypeCode => TypeCode.Object;
        public override EncodedValueType EncodedType => EncodedValueType.Object;

        private Stack<EncodedValue>? stack;

        private EncodedObject(IEncodable obj, Stack<EncodedValue> stack)
        {
            this.stack = stack;
            Value = obj;
        }

        private EncodedObject(IEncodable obj)
        {
            Value = obj;
            stack = null;
        }

        internal override void WriteBytes(BinaryWriter bw)
        {
            if (stack is null)
            {
                var handle = new EncodingHandle();

                ((IEncodable)Value).DoEncode(handle);

                stack = handle.stack;
            }

            var type = Value.GetType();

            if (!TypeRegister.ContainsValue(type)) { throw new EncodingException($"Type ({type.FullName}) is not registered."); }

            bw.Write(type.Name);
            bw.Write(stack.Count);
            
            while (stack.TryPop(out EncodedValue ev))
            {
                bw.Write((byte)ev.EncodedType);
                bw.Write((int)ev.TypeCode);
                ev.WriteBytes(bw);
            }
        }

        public static EncodedObject Create(IEncodable encodable)
        {
            var handle = new EncodingHandle();

            encodable.DoEncode(handle);

            return new EncodedObject(encodable, handle.stack);
        }

        
        public static void RegisterAll(Assembly assembly)
        {
            foreach (var type in SafeGetTypes(assembly))
            {
                if (!EncodingType.IsAssignableFrom(type))
                    continue;

                if (type == EncodingType)
                    continue;

                if (!type.IsClass && !type.IsValueType)
                    continue;

                if (type.IsAbstract)
                    continue;

                if (type.ContainsGenericParameters)
                    continue;

                if (TypeRegister.ContainsValue(type))
                    continue;

                TryAdd(type);
            }
        }

        private static IEnumerable<Type> SafeGetTypes(Assembly assembly)
        {
            try
            {
                return assembly.GetTypes();
            }
            catch (ReflectionTypeLoadException ex)
            {
                return ex.Types.Where(t => !(t is null))!;
            }
        }

        public static void RegisterTypes(params Type[] types)
        {
            foreach (Type t in types)
            {
                if (TypeRegister.ContainsValue(t)) { continue; }
                else { TryAdd(t); }
            }
        }

        private static void TryAdd(Type type)
        {
            if (type.GetConstructor(ConstructorType) is null)
            {
                throw new EncodingException("Registered types must have a constructor which accepts a DecodingHandle as an argument.");
            }
            else if (!EncodingType.IsAssignableFrom(type))
            {
                throw new EncodingException("Registered types must implement IEncodable.");
            }
            else if (type.IsAbstract)
            {
                throw new EncodingException("Abstract types cannot be registered.");
            }
            else if (!type.IsClass && !type.IsValueType)
            {
                throw new EncodingException("Type is not valid.");
            }
            else if (type.ContainsGenericParameters)
            {
                throw new EncodingException("Generic types cannot be registered.");
            }
            
            try { TypeRegister.Add(type.Name, type); }
            catch (ArgumentException)
            {
                throw new EncodingException("Two types cannot be registered with the same name.");
            }
        }

        internal static EncodedObject ReadOject(BinaryReader br)
        {
            var name = br.ReadString();

            if (!TypeRegister.ContainsKey(name)) { throw new EncodingException($"The type name read ({name}) was not registered."); }

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
                        stack.Push(ReadOject(br));
                        break;
                }
            }

            var type = TypeRegister[name];

            var cons = type.GetConstructor(ConstructorType) ?? throw new EncodingException("Registered types must have a constructor which accepts a DecodingHandle as an argument.");

            var dh = new DecodingHandle(stack);

            var ret = (IEncodable)cons.Invoke(new object[] { dh });

            return new EncodedObject(ret);
        }
    }
}