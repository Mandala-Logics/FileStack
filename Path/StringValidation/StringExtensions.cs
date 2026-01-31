using System;
using System.Linq;
using System.Collections.Generic;

namespace MandalaLogics.StringValidation
{
    [Flags]
    public enum CharachterClass 
    {
        Invalid = 0, 
        Whitespace = 0b0000_0001, 
        LowerCase = 0b1_0000_0010, 
        UpperCase = 0b1_0000_0100, 
        Number = 0b0000_1000, 
        WordChar = 0b10_0001_0000, 
        FileNameChar = 0b10_0010_0000, 
        NumberChar = 0b10_0100_0000, 
        Additional = 0b10_1000_0000,
        Letter = 0b1_0000_0000,
        Charchter = 0b10_0000_0000
    }

    public static class StringExtensions
    {
        //CHAR EXTENSIONS
        public static CharachterClass GetClass(this char c, char[] additional)
        {
            if (char.IsWhiteSpace(c)) { return CharachterClass.Whitespace; }
            else if (char.IsLower(c)) { return CharachterClass.LowerCase; }
            else if (char.IsUpper(c)) { return CharachterClass.UpperCase; }
            else if (char.IsNumber(c)) { return CharachterClass.Number; }

            CharachterClass ret = CharachterClass.Invalid;

            if (additional?.Contains(c) ?? false) { ret |= CharachterClass.Additional; }

            if (StringTemplate.FileNameChars.Contains(c)) { ret |= CharachterClass.FileNameChar; }

            if (StringTemplate.NumberChars.Contains(c)) { ret |= CharachterClass.NumberChar; }

            if (StringTemplate.WordChars.Contains(c)) { ret |= CharachterClass.WordChar; }

            return ret;
        }
        public static bool HasClass(this char c, CharachterClass flag) => (c.GetClass(null) & flag) != 0;

        //STRING EXTENSIONS
        public static int CaseInsensitiveHash(this IEnumerable<string> en)
        {
            var ret = 0;
            int x = 1;

            unchecked
            {
                foreach (string s in en)
                {
                    ret += (++x) * StringComparer.OrdinalIgnoreCase.GetHashCode(s);
                }
            }

            return ret;
        }
        public static byte[] GetFixedBytes(this string s, int byteLength)
        {
            var b = System.Text.Encoding.UTF8.GetBytes(s);

            if (b.Length > byteLength)
            {
                throw new InvalidCastException($"This string cannot be fully cast into {byteLength} bytes.");
            }
            else if (b.Length == byteLength)
            {
                return b;
            }
            else
            {
                var ret = new byte[byteLength];

                b.CopyTo(ret, 0);

                return ret;
            }
        }
        public static byte[] GetFixedBytes(this string s) => s.GetFixedBytes(System.Text.Encoding.UTF8.GetMaxByteCount(s.Length));
        public static int GetByteLength(this string s) => System.Text.Encoding.UTF8.GetByteCount(s);
    }
}
