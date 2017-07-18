using System;

namespace OmniSharp.MSBuild.SolutionParsing
{
    internal sealed class SolutionFile
    {
        private SolutionFile()
        {
        }

        public static SolutionFile Parse(string text)
        {
            if (text == null)
            {
                throw new ArgumentNullException(nameof(text));
            }

            return new SolutionFile();
        }
    }
}
