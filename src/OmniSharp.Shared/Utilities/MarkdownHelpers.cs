using System.Text.RegularExpressions;

namespace OmniSharp.Utilities
{
    public static class MarkdownHelpers
    {
        private static Regex EscapeRegex = new Regex(@"([\\`\*_\{\}\[\]\(\)#+\-\.!])", RegexOptions.Compiled);

        public static string Escape(string markdown)
        {
            if (markdown == null)
                return null;
            return EscapeRegex.Replace(markdown, @"\$1");
        }
    }
}
