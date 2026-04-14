using System;

namespace MandalaLogics.Encoding
{
    public sealed class TypeRegisterEntry
    {
        public Type Type { get; }
        public string Key { get; }

        public TypeRegisterEntry(Type type, string key)
        {
            Type = type;
            Key = key;
        }
    }
}