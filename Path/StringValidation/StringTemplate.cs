using System;
using System.Collections.Generic;
using System.Text;
using System.Text.RegularExpressions;
using MandalaLogics.Encoding;

namespace MandalaLogics.StringValidation
{
    [Flags]
    public enum StringValidationFlags : int
    {
        /// <summary>
        /// No flags specified, not allowed.
        /// </summary>
        None = 0,
        /// <summary>
        /// Automatically capitalizes the first letter of a string.
        /// </summary>
        AutoCapatliseFirst = 0b1,
        /// <summary>
        /// Automatically capitalizes the first letter of each word, as in a name.
        /// </summary>
        AutoCapatiliseEachWord = 0b10,
        /// <summary>
        /// Trims the start and end of the string automatically.
        /// </summary>
        AutoTrim = 0b100,
        /// <summary>
        /// Allows chrachters permitted in Windows filenames.
        /// </summary>
        AllowFileNameChars = 0b1_0000,
        /// <summary>
        /// Specifies that the starting must start with a letter, not a number or chrachter.
        /// </summary>
        MustStartWithLetter = 0b10_0000,
        /// <summary>
        /// Allows the use of lower-case and upper-case letters.
        /// </summary>
        AllowLetters = 0b100_0000,
        /// <summary>
        /// Allows number characters (0-9).
        /// </summary>
        AllowNumbers = 0b1000_0000,
        /// <summary>
        /// Allows the string to empty or whitespace, but not null.
        /// </summary>
        AllowEmpty = 0b1_0000_0000,
        /// <summary>
        /// Trims extra spaces between words.
        /// </summary>
        AutoTrimEachWord = 0b10_0000_0000,
        /// <summary>
        /// Allows ' and - for usernames etc.
        /// </summary>
        AllowWordChars = 0b100_0000_0000,
        /// <summary>
        /// Allows the characters . and -
        /// </summary>
        AllowNumberChars = 0b1000_0000_0000,
        /// <summary>
        /// For variable names.
        /// </summary>
        VariableName = Alphanumeric | MustStartWithLetter | AutoTrim,
        /// <summary>
        /// For archive names, usernames etc. No spaces. Allows hypens and single quote mark (apostophe).
        /// </summary>
        UserName = Alphanumeric | MustStartWithLetter | AllowWordChars | AutoTrim,
        /// <summary>
        /// Allows letters, numbers and filename chars.
        /// </summary>
        FileName = Alphanumeric | AllowFileNameChars | AutoTrim,
        /// <summary>
        /// Allows letters, auto capitalizes each word in the string and automatically trims each word.
        /// </summary>
        ProperName = AllowWordChars | AllowLetters | AutoCapatiliseEachWord | AutoTrimEachWord,
        /// <summary>
        /// Allows letters and numbers.
        /// </summary>
        Alphanumeric = AllowLetters | AllowNumbers,
        /// <summary>
        /// Allows letters, only one word allowed, auto-trims and automatically capitalizes the first letter of the string.
        /// </summary>
        ProperNoun = AllowWordChars | AllowLetters | AutoTrim | AutoCapatliseFirst,
        /// <summary>
        /// Allows numbers with number characters, for e.g. -9.872
        /// </summary>
        Number = AllowNumbers | AllowNumberChars | AutoTrim
    }

    public sealed class StringTemplate : IEncodable, IEquatable<StringTemplate>
    {
        
        private static readonly System.Text.RegularExpressions.Regex wordSplitter = new System.Text.RegularExpressions.Regex(@"(?<word>[^\s]+)(?<spaces>\s+)?");

        
        public static readonly char[] FileNameChars = new char[] { '-', '_', '.', ')', '(', '[', ']', '&', ':', ';', '+', '-', '=', '@', '\'', '%', '£', '$', '#', '*' };
        public static readonly char[] WordChars = new char[] { '\'', '-', '_' };
        public static readonly char[] NumberChars = new char[] { '.', '-' };
        public static StringTemplate VariableName { get; } = new StringTemplate(StringValidationFlags.VariableName, maxWords: 1);       
        public static StringTemplate FileName { get; } = new StringTemplate(StringValidationFlags.FileName);
        public static StringTemplate FileExtension { get; } = new StringTemplate(StringValidationFlags.Alphanumeric | StringValidationFlags.AutoTrim, maxWords: 1);
        public static StringTemplate ProperName { get; } = new StringTemplate(StringValidationFlags.ProperName);
        public static StringTemplate ProperNoun { get; } = new StringTemplate(StringValidationFlags.ProperNoun, maxWords: 1);
        public static StringTemplate UserName { get; } = new StringTemplate(StringValidationFlags.UserName, maxWords: 1);
        public static StringTemplate Number { get; } = new StringTemplate(StringValidationFlags.Number, maxWords: 1, format: new System.Text.RegularExpressions.Regex(@"^(-)?(\d+)((\.)(\d+))?(\s+)?$"));

        public StringValidationFlags Flags { get; }
        public System.Text.RegularExpressions.Regex FormatRegex { get; }
        public char[] Addititional { get; }
        public int MaxWords { get; }
        public int MinWords { get; }
        public int MaxLength { get; }
        public int MinLength { get; }
        public bool HasUpperWordLimit => MaxWords > 0;
        public bool HasLowerWordLimit => MinWords > 0;
        public bool HasUpperLengthLimit => MaxLength > 0;
        public bool HasLowerLengthLimit => MinLength > 0;
        public bool AllowEmpty => (Flags & StringValidationFlags.AllowEmpty) != 0;
        public bool AutoCapatliseFirst => (Flags & StringValidationFlags.AutoCapatliseFirst) != 0;
        public bool AutoCapatiliseEachWord => (Flags & StringValidationFlags.AutoCapatiliseEachWord) != 0;
        public bool AutoTrim => (Flags & StringValidationFlags.AutoTrim) != 0;
        public bool AllowFileNameChars => (Flags & StringValidationFlags.AllowFileNameChars) != 0;
        public bool MustStartWithLetter => (Flags & StringValidationFlags.MustStartWithLetter) != 0;
        public bool AllowLetters => (Flags & StringValidationFlags.AllowLetters) != 0;
        public bool AllowNumbers => (Flags & StringValidationFlags.AllowNumbers) != 0;
        public bool AutoTrimEachWord => (Flags & StringValidationFlags.AutoTrimEachWord) != 0;
        public bool AllowWordChars => (Flags & StringValidationFlags.AllowWordChars) != 0;
        public bool AllowNumberChars => (Flags & StringValidationFlags.AllowNumberChars) != 0;
        
        public StringTemplate(StringValidationFlags flags, int maxWords = -1, int minWords = -1, int maxLength = -1, int minLength = -1,
            System.Text.RegularExpressions.Regex format = default, params char[] additional)
        {
            if (flags == StringValidationFlags.None) throw new ArgumentNullException("String validation flags cannot be null.");
            else if (maxWords == 0 || minWords == 0 || maxLength == 0 || minLength == 0) throw new ArgumentException("Words/chrachter limits are not allowed to be zero.");

            MaxWords = maxWords;
            MinWords = minWords;
            MaxLength = maxLength;
            MinLength = minLength;
            Flags = flags;
            Addititional = additional;
            FormatRegex = format;
        }
        public StringTemplate(DecodingHandle handle)
        {
            Flags = (StringValidationFlags)handle.Next<int>();
            Addititional = handle.Next<char[]>();
            MinWords = handle.Next<int>();
            MaxWords = handle.Next<int>();
            MinLength = handle.Next<int>();
            MaxLength = handle.Next<int>();

            var s = handle.Next<string>();

            if (!string.IsNullOrEmpty(s)) { FormatRegex = new System.Text.RegularExpressions.Regex(s); }
            else { FormatRegex = default; }
        }

        
        public string Validate(string s)
        {
            if (Flags == StringValidationFlags.None) throw new NullReferenceException("Validation flags cannot be empty.");

            if (s is null)
            {
                if (AllowEmpty)
                {
                    CheckLengths(0, 0);
                    return string.Empty;
                }
                else throw new StringValidationException(StringExceptionReason.StringIsEmpty);
            }

            if (string.IsNullOrWhiteSpace(s))
            {
                if (AllowEmpty)
                {   
                    if (AutoTrim || AutoTrimEachWord)
                    {
                        CheckLengths(0, 0);
                        return string.Empty;
                    }
                    else
                    {
                        CheckLengths(0, s.Length);
                        return s;
                    }
                }
                else throw new StringValidationException(StringExceptionReason.StringIsEmpty);
            }

            s = s.TrimStart();

            if (MustStartWithLetter && !s[0].HasClass(CharachterClass.Letter))
            {
                throw new StringValidationException(StringExceptionReason.MustStartWithLetter);
            }            

            var matches = wordSplitter.Matches(s);

            if (HasLowerWordLimit && matches.Count < MinWords) throw new StringValidationException(StringExceptionReason.TooFewWords);

            if (HasUpperWordLimit && matches.Count > MaxWords) throw new StringValidationException(StringExceptionReason.TooManyWords);

            string[] arr = new string[matches.Count * 2];
            char[] chars;
            int x = 0;

            foreach (Match m in matches)
            {
                if (AutoCapatiliseEachWord || (AutoCapatliseFirst && x == 0))
                {
                    chars = m.Groups[1].Value.ToCharArray();
                    chars[0] = char.ToUpper(chars[0]);
                    arr[x] = new string(chars);
                }
                else
                {
                    arr[x] = m.Groups[1].Value;
                }

                x++;

                if (m.Groups[2].Success)
                {
                    if (AutoTrimEachWord)
                    {
                        arr[x] = " ";
                    }
                    else
                    {
                        arr[x] = m.Groups[2].Value;
                    }
                }

                x++;
            }

            s = string.Join("", arr);

            if (AutoTrim || AutoTrimEachWord) { s = s.TrimEnd(); }

            if (HasLowerLengthLimit && s.Length < MinLength) throw new StringValidationException(StringExceptionReason.StringTooShort);

            if (HasUpperLengthLimit && s.Length > MaxLength) throw new StringValidationException(StringExceptionReason.StringTooLong);

            CharachterClass cc;

            foreach (char c in s)
            {
                cc = c.GetClass(Addititional);

                if (cc == CharachterClass.Invalid) throw new StringValidationException("Invalid chrachter: " + c, StringExceptionReason.InvalidCharachter);
                else if (cc == CharachterClass.Whitespace) continue;
                else if ((cc & CharachterClass.Charchter) != 0)
                {
                    if (!((cc & CharachterClass.FileNameChar) == CharachterClass.FileNameChar && AllowFileNameChars) &&
                        !((cc & CharachterClass.WordChar) == CharachterClass.WordChar && AllowWordChars) &&
                        !((cc & CharachterClass.NumberChar) == CharachterClass.NumberChar && AllowNumberChars))
                    { throw new StringValidationException("Invalid chrachter: " + c, StringExceptionReason.InvalidCharachter); }
                }
                else
                {
                    if (!((cc & CharachterClass.Letter) != 0 && AllowLetters) &&
                        !((cc & CharachterClass.Number) != 0 && AllowNumbers))
                    { throw new StringValidationException("Invalid chrachter: " + c, StringExceptionReason.InvalidCharachter); }
                }
            }

            if (!(FormatRegex?.IsMatch(s) ?? true)) throw new StringValidationException(StringExceptionReason.IncorrectFormat);

            return s;
        }
        public bool TryValidate(string s, out string ret)
        {
            try { ret = Validate(s); return true; }
            catch (StringValidationException) { ret = string.Empty; return false; }
        }
        public StringValidationException CatchValidate(string s, out string ret)
        {
            try { ret = Validate(s); return null; }
            catch (StringValidationException e) { ret = string.Empty; return e; }
        }
        public void CheckLengths(int wordcount, int length)
        {
            if (HasLowerWordLimit && wordcount < MinWords) throw new StringValidationException(StringExceptionReason.TooFewWords);

            if (HasUpperWordLimit && wordcount > MaxWords) throw new StringValidationException(StringExceptionReason.TooManyWords);

            if (HasLowerLengthLimit && length < MinLength) throw new StringValidationException(StringExceptionReason.StringTooShort);

            if (HasUpperLengthLimit && length > MaxLength) throw new StringValidationException(StringExceptionReason.StringTooLong);
        }
        void IEncodable.DoEncode(EncodingHandle handle)
        {
            handle.Append((int)Flags);
            handle.Append(Addititional);
            handle.Append(MinWords);
            handle.Append(MaxWords);
            handle.Append(MinLength);
            handle.Append(MaxLength);
            handle.Append(FormatRegex.ToString());
        }
        public List<char> GetAllowedSpecialChars()
        {
            List<char> ls = new List<char>();

            if (AllowWordChars) { ls.AddRange(WordChars); }
            
            if (AllowFileNameChars) { ls.AddRange(FileNameChars); }

            if (AllowNumberChars) { ls.AddRange(NumberChars); }

            if (Addititional is object) { ls.AddRange(Addititional); }

            return ls;
        }
        public string GetMessage()
        {
            var sb = new StringBuilder("Enter text");

            int c = (HasUpperWordLimit ? 1 : 0) + (HasLowerWordLimit ? 1 : 0) + (HasUpperLengthLimit ? 1 : 0) + (HasLowerLengthLimit ? 1 : 0);
            int x = 0;

            if (HasUpperWordLimit)
            {
                x++;

                sb.Append($" which is no longer than {MaxWords} word(s)");
            }

            if (HasLowerWordLimit)
            {
                x++;

                if (x == 0) sb.Append($" which is at least {MinWords} word(s)");
                else if (x == c) sb.Append($" and which is no longer than {MinWords} word(s)");
                else sb.Append($", no longer than {MinWords} word(s)");
            }

            if (HasUpperLengthLimit)
            {
                x++;

                if (x == 0) sb.Append($" which is no longer than {MaxLength} charachter(s)");
                else if (x == c) sb.Append($" and which is no longer than {MaxLength} chrachter(s)");
                else sb.Append($", no longer than {MaxLength} chrachter(s)");                
            }

            if (HasLowerLengthLimit)
            {
                x++;

                if (x == 0) sb.Append($" which is longer than {MinLength} charachter(s)");
                else if (x == c) sb.Append($" and which is longer than {MinLength} charachter(s)");
            }

            sb.Append(". ");

            if (AllowNumbers) sb.Append("Numbers allowed. ");
            if (AllowLetters) sb.Append("Letters allowed. ");
            if (AllowEmpty) sb.Append("May be empty. ");
            if (MustStartWithLetter) sb.Append("Must start with a letter. ");

            var ls = GetAllowedSpecialChars();

            if (ls.Count > 0) 
            { 
                sb.Append("The following special charachters are allowed: ");
                sb.Append(new string(ls.ToArray()));
            }

            return sb.ToString();
        }

        
        public override bool Equals(object obj)
        {
            return obj is StringTemplate template && Equals(template);
        }
        public bool Equals(StringTemplate other)
        {
            return Flags == other.Flags &&
                   EqualityComparer<System.Text.RegularExpressions.Regex>.Default.Equals(FormatRegex, other.FormatRegex) &&
                   EqualityComparer<char[]>.Default.Equals(Addititional, other.Addititional) &&
                   MaxWords == other.MaxWords &&
                   MinWords == other.MinWords &&
                   MaxLength == other.MaxLength &&
                   MinLength == other.MinLength;
        }
        public override int GetHashCode()
        {
            int hashCode = -1347504946;
            hashCode = hashCode * -1521134295 + Flags.GetHashCode();
            hashCode = hashCode * -1521134295 + EqualityComparer<System.Text.RegularExpressions.Regex>.Default.GetHashCode(FormatRegex);
            hashCode = hashCode * -1521134295 + EqualityComparer<char[]>.Default.GetHashCode(Addititional);
            hashCode = hashCode * -1521134295 + MaxWords.GetHashCode();
            hashCode = hashCode * -1521134295 + MinWords.GetHashCode();
            hashCode = hashCode * -1521134295 + MaxLength.GetHashCode();
            hashCode = hashCode * -1521134295 + MinLength.GetHashCode();
            return hashCode;
        }
    }
}
