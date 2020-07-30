using System.Linq;
using Xunit;

namespace TestUtility
{
    public static class AssertUtils
    {
        public static void AssertIgnoringIndent(string expected, string actual)
        {
            Assert.Equal(TrimLines(expected), TrimLines(actual), false, true, true);
        }

        public static void AssertIgnoringIndentAndNewlines(string expected, string actual)
        {
            Assert.Equal(TrimAndRemoveNewLines(expected), TrimAndRemoveNewLines(actual), false, true, true);
        }

        private static string TrimAndRemoveNewLines(string source)
        {
            return string.Join("", source.Split('\n').Select(s => s.Trim()));
        }

        private static string TrimLines(string source)
        {
            return string.Join("\n", source.Split('\n').Select(s => s.Trim()));
        }
    }
}