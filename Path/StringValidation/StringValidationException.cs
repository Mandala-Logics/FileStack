using System;

namespace MandalaLogics.StringValidation
{
    public enum StringExceptionReason : int
    {
        Other, TooManyWords, TooFewWords, StringTooLong, StringTooShort, NoSpacesAllowed, 
        NumbersNotAllowed, LettersNotAllowed, StringIsEmpty, InvalidCharachter,
        MustStartWithLetter, IncorrectFormat, TooManyBytes
    }

    public class StringValidationException : Exception
    {
        public StringExceptionReason Reason { get; }

        public StringValidationException(string message) : base(message) { Reason = StringExceptionReason.Other; }

        public StringValidationException(string message, StringExceptionReason reason) : base(message) { Reason = reason; }

        public StringValidationException(StringExceptionReason reason) : base(GetMessage(reason)) { Reason = reason; }

        public static string GetMessage(StringExceptionReason reason)
        {
            switch (reason)
            {                
                case StringExceptionReason.TooManyWords:
                    return "Maximum number of words exceeded.";
                case StringExceptionReason.TooFewWords:
                    return "Not enough words.";
                case StringExceptionReason.StringTooLong:
                    return "Maximum number of chrachters exceeded.";
                case StringExceptionReason.StringTooShort:
                    return "Too few chrachters.";
                case StringExceptionReason.NoSpacesAllowed:
                    return "Spaces are not allowed.";
                case StringExceptionReason.NumbersNotAllowed:
                    return "Numbers are not allowed.";
                case StringExceptionReason.LettersNotAllowed:
                    return "Letters are not allowed.";
                case StringExceptionReason.StringIsEmpty:
                    return "Cannot only be whitespace, empty or null.";
                case StringExceptionReason.InvalidCharachter:
                    return "Invalid charachter(s).";
                case StringExceptionReason.MustStartWithLetter:
                    return "String must start with a letter chrachter.";
                case StringExceptionReason.IncorrectFormat:
                    return "String is not in the correct format.";
                case StringExceptionReason.Other:
                default:
                    return "Invalid string.";
            }
        }
    }
}
