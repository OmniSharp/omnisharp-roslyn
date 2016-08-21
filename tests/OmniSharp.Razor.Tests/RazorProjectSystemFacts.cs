using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Text;
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

        public static async Task<Result> CreateTestWorkspace(string testFolder)
        {
            var testProject = TestsContext.Default.GetTestSample("RazorProjectSample01");
            var env = new FakeEnvironment { Path = testProject };
            var host = TestHelpers.CreatePluginHost(new[] { typeof(RazorWorkspace).GetTypeInfo().Assembly });
            var workspace = await TestHelpers.CreateSimpleWorkspace(host, new Dictionary<string, string>());
            var razorWorkspace = new RazorWorkspace(env, workspace, new FakeLoggerFactory());
            var projectSystem = new RazorProjectSystem(env, workspace, razorWorkspace, new FakeLoggerFactory(), new NullEventEmitter());

            projectSystem.Initalize(new ConfigurationBuilder().Build());

            return new Result()
            {
                Path = testProject,
                OmnisharpWorkspace = workspace,
                RazorWorkspace = razorWorkspace,
                ProjectSystem = projectSystem
            };
        }

        public static async Task<Result> CreateTestWorkspace(string testFolder, Dictionary<string, string> files)
        {
            var result = await CreateTestWorkspace(testFolder);

            var id = ProjectId.CreateNewId();
            var info = ProjectInfo.Create(
                id: id,
                version: VersionStamp.Create(),
                name: $"Razor",
                assemblyName: "Razor",
                language: RazorLanguage.Razor);

            result.OmnisharpWorkspace.AddProject(info);

            foreach (var file in files)
            {
                var sourceText = SourceText.From(file.Value, encoding: Encoding.UTF8);
                var docId = DocumentId.CreateNewId(id);
                var version = VersionStamp.Create();

                var loader = TextLoader.From(TextAndVersion.Create(sourceText, version));

                var doc = DocumentInfo.Create(docId, file.Key, filePath: file.Key, loader: loader);
                result.OmnisharpWorkspace.AddDocument(doc);
            }

            return result;
        }
    }
    public class RazorProjectSystemFacts
    {
        [Fact]
        public async Task Fact()
        {
            var result = await RazorTestHelpers.CreateTestWorkspace("RazorProjectSample01");

            var docs = result.OmnisharpWorkspace.GetDocuments(Path.Combine(result.Path, "Test.cshtml"));
            Assert.Equal(1, docs.Count());
        }
    }
}
