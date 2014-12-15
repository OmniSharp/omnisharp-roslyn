using System;
using Microsoft.Framework.Logging;
using OmniSharp.Services;
using Xunit;

namespace OmniSharp.Tests
{
    public class OmnisharpEnvironmentFacts
    {
        [Fact]
        public void OmnisharpEnvironmentSetsPathsCorrectly()
        {
            var environment = new OmnisharpEnvironment(@"c:\foo\src\foo.sln", TraceType.Information);

            Assert.Equal(@"c:\foo\src\foo.sln", environment.SolutionFilePath);
            Assert.Equal(@"c:\foo\src", environment.Path);
        }

        [Fact]
        public void OmnisharpEnvironmentHasNullSolutionFilePathIfDirectorySet()
        {
            var environment = new OmnisharpEnvironment(@"c:\foo\src\", TraceType.Information);

            Assert.Null(environment.SolutionFilePath);
            Assert.Equal(@"c:\foo\src\", environment.Path);
        }
    }
}
