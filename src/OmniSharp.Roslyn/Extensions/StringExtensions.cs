using System;
using System.Linq;

namespace OmniSharp.Extensions
{
    public static class StringExtensions
    {
        public static bool IsValidCompletionFor(this string completion, string partial)
        {
            return completion.IsValidCompletionStartsWithIgnoreCase(partial) || completion.IsSubsequenceMatch(partial);
        }

        public static bool IsValidCompletionStartsWithExactCase(this string completion, string partial)
        {
            return completion.StartsWith(partial);
        }

        public static bool IsValidCompletionStartsWithIgnoreCase(this string completion, string partial)
        {
            return completion.StartsWith(partial, StringComparison.OrdinalIgnoreCase);
        }

        public static bool IsCamelCaseMatch(this string completion, string partial)
        {
            return new string(completion.Where(c => c >= 'A' && c <= 'Z').ToArray()).StartsWith(partial, StringComparison.OrdinalIgnoreCase);
        }

        public static bool IsSubsequenceMatch(this string completion, string partial)
        {
            if (partial == string.Empty)
            {
                return true;
            }

            if (partial.Length > 1 && completion.IndexOf(partial, StringComparison.InvariantCultureIgnoreCase) >= 0)
            {
                return true;
            }

            // Limit the number of results returned by making sure
            // at least the first characters match.
            // We can get far too many results back otherwise.
            if (!FirstLetterMatches(partial, completion))
            {
                return false;
            }

            return new string(completion.ToUpper().Intersect(partial.ToUpper()).ToArray()) == partial.ToUpper();
        }

        private static bool FirstLetterMatches(string word, string match)
        {
            if (string.IsNullOrEmpty(match))
            {
                return false;
            }

            return char.ToLowerInvariant(word[0]) == char.ToLowerInvariant(match[0]);
        }
    }
}
