﻿using System;
using Microsoft.Framework.Logging;
using OmniSharp.Services;
using Xunit;

namespace OmniSharp.Tests
{
    public class OmnisharpEnvironmentFacts
    {

        [Fact]
        public void OmnisharpEnvironmentSetsSolutionPathCorrectly()
        {
            var environment = new OmnisharpEnvironment(@"foo.sln", 1000, LogLevel.Information);
            Assert.Equal(@"foo.sln", environment.SolutionFilePath);
        }
        [Fact]
        public void OmnisharpEnvironmentSetsPathCorrectly()
        {
            var environment = new OmnisharpEnvironment(@"foo.sln", 1000, LogLevel.Information);
            Assert.Equal(@"", environment.Path);
        }

        [Fact]
        public void OmnisharpEnvironmentSetsPortCorrectly()
        {
            var environment = new OmnisharpEnvironment(@"foo.sln", 1000, LogLevel.Information);
            Assert.Equal(1000, environment.Port);
        }

        [Fact]
        public void OmnisharpEnvironmentHasNullSolutionFilePathIfDirectorySet()
        {
            var environment = new OmnisharpEnvironment(@"c:\foo\src\", 1000, LogLevel.Information);

            Assert.Null(environment.SolutionFilePath);
        }
    }
}
