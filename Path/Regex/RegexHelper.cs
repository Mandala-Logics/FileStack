namespace MandalaLogics.StringValidation.Regex
{
    public static class RegexHelper
    {
        public static bool WildcardMatch(string text, string pattern)
        {
            int t = 0, p = 0, star = -1, match = 0;

            while (t < text.Length)
            {
                if (p < pattern.Length && 
                    (pattern[p] == text[t]))
                {
                    t++;
                    p++;
                }
                else if (p < pattern.Length && pattern[p] == '*')
                {
                    star = p++;
                    match = t;
                }
                else if (star != -1)
                {
                    p = star + 1;
                    t = ++match;
                }
                else
                {
                    return false;
                }
            }

            while (p < pattern.Length && pattern[p] == '*')
                p++;

            return p == pattern.Length;
        }
    }
}