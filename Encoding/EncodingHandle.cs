using System;
using System.Collections;
using System.Collections.Generic;

namespace MandalaLogics.Encoding
{
    public sealed class EncodingHandle
    {
        internal readonly Stack<EncodedValue> stack = new Stack<EncodedValue>();

        internal EncodingHandle() { }

        public void Append(string value)
        {
            stack.Push(new EncodedPrimitive(value));
        }

        public void Append(EncodedValue value)
        {
            stack.Push(value);
        }

        public void Append(IConvertible value)
        {
            stack.Push(new EncodedPrimitive(value));
        }

        public void Append(Array value)
        {
            stack.Push(EncodedArray.Create(value));
        }

        public void Append(IEnumerable value)
        {
            stack.Push(EncodedArray.Create(value));
        }

        public void Append(IEncodable value)
        {
            stack.Push(EncodedObject.Create(value));
        }
    }
}