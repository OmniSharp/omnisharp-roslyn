using Moq;
using OmniSharp.ConfigurationManager;
using OmniSharp.Models;
using OmniSharp.Models.BuildCommand;
using OmniSharp.Roslyn.CSharp.Services;
using OmniSharp.Roslyn.CSharp.Services.BuildCommand;
using OmniSharp.Roslyn.Services;
using System;
using System.Linq;
using System.Threading.Tasks;
using TestUtility;
using Xunit;
using Xunit.Abstractions;

namespace OmniSharp.Roslyn.CSharp.Tests
{
    public class BuildCommandFacts : AbstractSingleRequestHandlerTestFixture<BuildCommandService>
    {
        protected override string EndpointName => OmniSharpEndpoints.BuildCommand;

        public BuildCommandFacts(ITestOutputHelper output, SharedOmniSharpHostFixture sharedOmniSharpHostFixture)
            : base(output, sharedOmniSharpHostFixture)
        {
            OmniSharpConfiguration config = new OmniSharpConfiguration();
            OmniSharpConfiguration omniSharpConfiguration = new OmniSharpConfiguration()
            {
                MSBuildPath = new BuildPath()
                {
                    Path = "path\\to\\msbuild"
                }
            };
        }

        [Fact]
        public async Task GetBuildCommand()
        {
            const string source = @"
using NUnit.Framework;
using Should;

namespace TestApp.Tests
{
    [TestFixture]
    public class Tests
    {
        [Test]
        public void S$$hould_be_true()
        {
            true.ShouldBeTrue();
        }
    }
}
";

            OmniSharpConfiguration config = new OmniSharpConfiguration()
            {
                MSBuildPath = new BuildPath()
                {
                    Path = "path\\to\\msbuild"
                }
            };

            var runTest = await GetBuildCommandAsync(source);
            Assert.NotNull(runTest.First().Text);

        }

        private async Task<QuickFix[]> GetBuildCommandAsync(string source)
        {
            var testFile = new TestFile("dummy.cs", source);
            SharedOmniSharpTestHost.AddFilesToWorkspace(testFile);
            var rh = SharedOmniSharpTestHost.GetRequestHandler<BuildCommandService>(OmniSharpEndpoints.BuildCommand);
            var a = rh.Arguments;
            var cc = rh._config.MSBuildPath = new BuildPath()
            {
                Path = "path\\to\\msbuild"
            };
            

            var point = testFile.Content.GetPointFromPosition();
            var requestHandler = GetRequestHandler(SharedOmniSharpTestHost);
            var request = new BuildCommandRequest
            {
                Type = BuildType.Build,
                Line = point.Line,
                Column = point.Offset,
                FileName = testFile.FileName,
                Buffer = testFile.Content.Code
            };

            var response = await requestHandler.Handle(request);

            return response.QuickFixes.ToArray();
        }
    }
}
