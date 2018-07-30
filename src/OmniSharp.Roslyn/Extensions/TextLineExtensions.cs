using Microsoft.CodeAnalysis.Text;

namespace OmniSharp.Roslyn.Extensions
{
    public static class TextLineExtensions
    {
        public static bool StartsWith(this TextLine textLine, string text)
        {
            if (text.Length > textLine.Span.Length)
            {
                return false;
            }

            for (var (i, j) = (textLine.Start, 0); j < text.Length; i++)
            {
                if (textLine.Text[i] != text[j])
                {
                    return false;
                }

                j++;
            }

            return true;
        }
    }
}
