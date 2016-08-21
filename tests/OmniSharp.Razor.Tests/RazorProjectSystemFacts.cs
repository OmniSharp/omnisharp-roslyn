using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;
using Microsoft.Extensions.Configuration;
using OmniSharp.Roslyn;
using OmniSharp.Services;
using OmniSharp.Tests;
using TestCommon;
using TestUtility;
using TestUtility.Fake;
using Xunit;

namespace OmniSharp.Razor.Tests
{
    public static class RazorTestHelpers
    {
        public class Result
        {
            public string Path { get; set; }
            public OmnisharpWorkspace OmnisharpWorkspace { get; set; }
            public RazorWorkspace RazorWorkspace { get; set; }
            public RazorProjectSystem ProjectSystem { get; set; }

        }

        public static async Task<Result > CreateTestWorkspace(string testFolder)
        {
            var testProject = TestsContext.Default.GetTestSample("RazorProjectSample01");
            var env = new FakeEnvironment { Path = testProject };
            var host = TestHelpers.CreatePluginHost(new[] { typeof(RazorWorkspace).GetTypeInfo().Assembly });
            var workspace = await TestHelpers.CreateSimpleWorkspace(host, new Dictionary<string, string>());
            var razorWorkspace = new RazorWorkspace(env, workspace);
            var projectSystem = new RazorProjectSystem(env, workspace, razorWorkspace, new FakeLoggerFactory(), new NullEventEmitter());

            return new Result()
            {
                Path = testProject,
                OmnisharpWorkspace = workspace,
                RazorWorkspace = razorWorkspace,
                ProjectSystem = projectSystem
            };
        }
    }
    public class RazorProjectSystemFacts
    {
        [Fact]
        public async Task Fact()
        {
            var result = await RazorTestHelpers.CreateTestWorkspace("RazorProjectSample01");

            result.ProjectSystem.Initalize(new ConfigurationBuilder().Build());

            var docs = result.OmnisharpWorkspace.GetDocuments(Path.Combine(result.Path, "Test.cshtml"));
            Assert.Equal(1, docs.Count());
        }
    }
}
