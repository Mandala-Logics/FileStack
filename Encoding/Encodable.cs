using System;

namespace MandalaLogics.Encoding
{
    [AttributeUsage(AttributeTargets.Class | AttributeTargets.Struct, AllowMultiple = false, Inherited = false)]
    public class Encodable : Attribute
    {
        public string Key { get; }

        public Encodable(string key)
        {
            Key = key;
        }
    }
}