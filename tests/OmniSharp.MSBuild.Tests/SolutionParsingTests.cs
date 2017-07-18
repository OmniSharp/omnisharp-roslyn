using System;
using OmniSharp.MSBuild.SolutionParsing;
using Xunit;

namespace OmniSharp.MSBuild.Tests
{
    public class SolutionParsingTests
    {
        [Fact]
        public void SolutionFile_Parse_throws_with_null_text()
        {
            Assert.Throws<ArgumentNullException>(() => SolutionFile.Parse(null));
        }
    }
}
