using System.Text.RegularExpressions;

namespace OmniSharp.Roslyn.CSharp.Helpers
{
    public static class LspSnippetHelpers
    {
        private static Regex EscapeRegex = new Regex(@"([\\\$}])", RegexOptions.Compiled);

        /// <summary>
        /// Escape the given string for use as an LSP snippet. This escapes '\', '$', and '}'.
        /// </summary>
        public static string Escape(string snippet)
        {
            if (snippet == null)
                return null;
            return EscapeRegex.Replace(snippet, @"\$1");
        }
    }
}
