using System;
using System.Linq;
using System.Text;
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

        // Taken from dotnet/roslyn, MIT License.
        // https://github.com/dotnet/roslyn/blob/2834b74995bb66a7cb19cb09069c17812819afdc/src/Compilers/Test/Core/Assert/AssertEx.cs#L188-L203
        public static void Equal(string expected, string actual)
        {
            if (string.Equals(expected, actual, StringComparison.Ordinal))
            {
                return;
            }

            var message = new StringBuilder();
            message.AppendLine();
            message.AppendLine("Expected:");
            message.AppendLine(expected);
            message.AppendLine("Actual:");
            message.AppendLine(actual);

            Assert.Fail(message.ToString());
        }
    }
}
