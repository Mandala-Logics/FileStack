using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using MandalaLogics.Locking;

namespace MandalaLogics.Encoding
{
    public static class EncodingRegister
    {
        private static readonly LeasedList<TypeRegisterEntry> TypeRegister 
            = new LeasedList<TypeRegisterEntry>();
        
        public static readonly Type[] ConstructorType = { typeof(DecodingHandle) };
        public static readonly Type EncodingType = typeof(IEncodable);

        static EncodingRegister()
        {
            RegisterAll(Assembly.GetAssembly(typeof(EncodingRegister)));
        }

        public static bool IsRegistered(Type type)
        {
            using var l = TypeRegister.GetLease();
            
            foreach (var entry in l.Value)
            {
                if (entry.Type == type) return true;
            }

            return false;
        }

        public static Type? GetType(string key)
        {
            using var l = TypeRegister.GetLease();
            
            foreach (var entry in l.Value)
            {
                if (entry.Key == key) return entry.Type;
            }

            return null;
        }

        public static string? GetKey(Type type)
        {
            using var l = TypeRegister.GetLease();
            
            foreach (var entry in l.Value)
            {
                if (entry.Type == type) return entry.Key;
            }

            return null;
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

                if (IsRegistered(type))
                    continue;

                Add(type);
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
            foreach (var t in types)
            {
                if (IsRegistered(t)) { continue; }
                else { Add(t); }
            }
        }

        private static void Add(Type type)
        {
            if (type.GetConstructor(ConstructorType) is null)
            {
                throw new EncodingException($"Registered types must have a constructor which accepts a DecodingHandle as an argument, {type.Name} does not.");
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

            var attribute = type.GetCustomAttribute<Encodable>();

            if (attribute is null) 
                throw new EncodingException($"Registered types must use the Encodable attribute, {type.Name} does not.");

            if (GetType(attribute.Key) is { })
                throw new EncodingException($"A type with the key {attribute.Key} is already registered.");
            
            using var l = TypeRegister.GetLease();

            l.Value.Add(new TypeRegisterEntry(type, attribute.Key));
        }
    }
}