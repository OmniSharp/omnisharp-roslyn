using System;

namespace OmniSharp.MSBuild.SolutionParsing
{
    internal class InvalidSolutionFileException : Exception
    {
        public InvalidSolutionFileException()
        {
        }

        public InvalidSolutionFileException(string message) : base(message)
        {
        }

        public InvalidSolutionFileException(string message, Exception innerException) : base(message, innerException)
        {
        }
    }
}
