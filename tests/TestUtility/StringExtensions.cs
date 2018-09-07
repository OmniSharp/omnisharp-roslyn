using System.IO;

namespace TestUtility
{
    public static class StringExtensions
    {
        /// <summary>
        /// Given a file or directory path, return a path where all directory separators
        /// are replaced with a forward slash (/) character.
        /// </summary>
        public static string EnsureForwardSlashes(this string path)
            => path.Replace('\\', '/');
    }
}
