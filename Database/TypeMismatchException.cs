using System;
using MandalaLogics.Encoding;

namespace MandalaLogics.Database
{
    public sealed class TypeMismatchException : Exception
    {
        public TypeMismatchException(Type expectedType, Type actualType) : base(GetMessage(expectedType, actualType)) { }
        
        public TypeMismatchException(Type actualType, string expectedKey) : base(GetMessage(actualType, expectedKey)) { }

        private static string GetMessage(Type actualType, string expectedKey)
        {
            var actualKey = EncodingRegister.GetKey(actualType);
            
            if (actualKey is null)
                return $"{actualType.FullName} is not registered for encoding.";

            return $"The type read from the stream ({actualKey}), is not the same as the type with which this database was initially created ({expectedKey}).";
        }

        private static string GetMessage(Type expectedType, Type actualType)
        {
            var expectedKey = EncodingRegister.GetKey(expectedType);
            var actualKey = EncodingRegister.GetKey(actualType);

            if (expectedKey is null)
                return $"{expectedType.FullName} is not registered for encoding.";
            
            if (actualKey is null)
                return $"{actualType.FullName} is not registered for encoding.";
            
            return $"The type read from the stream ({actualKey}) did not match the type that was given in the generic argument ({expectedKey}).";
        }
    }
}