using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;

namespace MandalaLogics.StringValidation.Regex
{
    public sealed class WildcardRegexBuilder
    {
        public static char[] EscapeCharachters = new char[] { '.', '^', '$', '+', '?', '(', ')', '[', ']', '{', '}', '\\', '|' };

        public System.Text.RegularExpressions.Regex Regex { get; }

        public WildcardRegexBuilder(string pattern)
        {
            if (string.IsNullOrEmpty(pattern)) { throw new ArgumentNullException("pattern"); }

            var sb = new StringBuilder();

            for (int x = 0; x < pattern.Length; x++)
            {
                if (EscapeCharachters.Contains(pattern[x]))
                {
                    sb.Append('\\');
                    sb.Append(pattern[x]);
                }
                else if (pattern[x] == '*')
                {
                    sb.Append(".+");
                }
                else
                {
                    sb.Append(pattern[x]);
                }
            }

            Regex = new System.Text.RegularExpressions.Regex(sb.ToString());
        }

        public List<T> MatchList<T>(IEnumerable<T> list)
        {
            var ret = new List<T>();

            foreach (T obj in list)
            {
                if (obj is object)
                {
                    if (Regex.IsMatch(obj.ToString())) { ret.Add(obj); }
                }
            }

            return ret;
        }

        public bool IsMatch<T>(T obj)
        {
            if (obj is object)
            {
                return Regex.IsMatch(obj.ToString());
            }
            else { throw new ArgumentNullException("obj"); }
        }

        public Match Match<T>(T obj)
        {
            if (obj is object)
            {
                return Regex.Match(obj.ToString());
            }
            else { throw new ArgumentNullException("obj"); }
        }
    }
}