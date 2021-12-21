using System;

namespace OmniSharp.MSBuild.Discovery
{
    internal class MSBuildNotFoundException : Exception
    {
        public MSBuildNotFoundException()
        {
        }

        public MSBuildNotFoundException(string message) : base(message)
        {
        }

        public MSBuildNotFoundException(string message, Exception innerException) : base(message, innerException)
        {
        }
    }
}
