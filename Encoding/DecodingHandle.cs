using System;
using System.Collections.Generic;

namespace MandalaLogics.Encoding
{
    public sealed class DecodingHandle
    {
        internal Stack<EncodedValue> Stack;

        internal DecodingHandle(Stack<EncodedValue> stack)
        {
            Stack = stack;
        }

        public T Next<T>()
        {
            if (Stack.Count == 0) { throw new EncodingException("There are no more values to read; class must seralise and deserilise the same amount of values."); }

            var requestedType = typeof(T);

            var x = Stack.Pop();

            var valueType = x.Value.GetType();

            if (requestedType.IsGenericType)
            {
                throw new EncodingException("The type requested was a generic type; you can only request arrays from DecodingHandle.");
            }
            else if (!requestedType.IsAssignableFrom(valueType))
            {
                throw new EncodingException("Wrong type requested.");
            }
            else if (requestedType.IsEnum)
            {
                if (!Enum.IsDefined(requestedType, x.Value))
                {
                    throw new EncodingException("Invalid enum value read.");
                }
            }

            return (T)x.Value;
        }

        public EncodedValue Next()
        {
            return Stack.Pop();
        }
    }
}