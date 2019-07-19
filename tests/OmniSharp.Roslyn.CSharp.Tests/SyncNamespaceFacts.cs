using System.IO;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;
using Microsoft.CodeAnalysis;
using OmniSharp.Models;
using OmniSharp.Models.V2.CodeActions;
using OmniSharp.Roslyn.CSharp.Services.Refactoring.V2;
using TestUtility;
using Xunit;
using Xunit.Abstractions;

namespace OmniSharp.Roslyn.CSharp.Tests
{
    public class SyncNamespaceFacts : AbstractTestFixture
    {
        public SyncNamespaceFacts(ITestOutputHelper output)
            : base(output)
        {
        }

        [Theory]
        [InlineData("OmniSharpTest", "Bar.cs")]
        [InlineData("OmniSharpTest.Foo", "Foo", "Bar.cs")]
        [InlineData("OmniSharpTest.Foo.Bar", "Foo", "Bar", "Baz.cs")]
        public async Task RespectFolderName_InOfferedRefactorings(string expectedNamespace, params string[] relativePath)
        {
            var testFile = new TestFile(Path.Combine(TestAssets.Instance.TestFilesFolder, Path.Combine(relativePath)), @"namespace Xx$$x { }");

            using (var host = CreateOmniSharpHost(new[] { testFile }, null, path: TestAssets.Instance.TestFilesFolder))
            {
                var point = testFile.Content.GetPointFromPosition();
                var getRequestHandler = host.GetRequestHandler<GetCodeActionsService>(OmniSharpEndpoints.V2.GetCodeActions);
                var getRequest = new GetCodeActionsRequest
                {
                    Line = point.Line,
                    Column = point.Offset,
                    FileName = testFile.FileName
                };

                var getResponse = await getRequestHandler.Handle(getRequest);
                Assert.NotNull(getResponse.CodeActions);
                Assert.Contains(getResponse.CodeActions, f => f.Name == $"Change namespace to '{expectedNamespace}'");
            }
        }

        [Theory]
        [InlineData("LiveChanged", "Bar.cs")]
        [InlineData("LiveChanged.Foo", "Foo", "Bar.cs")]
        [InlineData("LiveChanged.Foo.Bar", "Foo", "Bar", "Baz.cs")]
        public async Task RespectFolderName_InOfferedRefactorings_AfterLiveChange(string expectedNamespace, params string[] relativePath)
        {
            var testFile = new TestFile(Path.Combine(TestAssets.Instance.TestFilesFolder, Path.Combine(relativePath)), @"namespace Xx$$x { }");

            using (var host = CreateOmniSharpHost(new[] { testFile }, null, path: TestAssets.Instance.TestFilesFolder))
            {
                var point = testFile.Content.GetPointFromPosition();
                var getRequestHandler = host.GetRequestHandler<GetCodeActionsService>(OmniSharpEndpoints.V2.GetCodeActions);

                var changedSolution = host.Workspace.CurrentSolution.WithProjectDefaultNamespace(host.Workspace.CurrentSolution.Projects.ElementAt(0).Id, "LiveChanged");
                host.Workspace.TryApplyChanges(changedSolution);

                var getRequest = new GetCodeActionsRequest
                {
                    Line = point.Line,
                    Column = point.Offset,
                    FileName = testFile.FileName
                };

                var getResponse = await getRequestHandler.Handle(getRequest);
                Assert.NotNull(getResponse.CodeActions);
                Assert.Contains(getResponse.CodeActions, f => f.Name == $"Change namespace to '{expectedNamespace}'");
            }
        }

        [Theory]
        [InlineData("OmniSharpTest", "Bar.cs")]
        [InlineData("OmniSharpTest.Foo", "Foo", "Bar.cs")]
        [InlineData("OmniSharpTest.Foo.Bar", "Foo", "Bar", "Baz.cs")]
        public async Task RespectFolderName_InExecutedCodeActions(string expectedNamespace, params string[] relativePath)
        {
            var expected = "namespace " + expectedNamespace + " { }";
            var testFile = new TestFile(Path.Combine(TestAssets.Instance.TestFilesFolder, Path.Combine(relativePath)), @"namespace Xx$$x { }");

            using (var host = CreateOmniSharpHost(new[] { testFile }, null, TestAssets.Instance.TestFilesFolder))
            {
                var point = testFile.Content.GetPointFromPosition();
                var runRequestHandler = host.GetRequestHandler<RunCodeActionService>(OmniSharpEndpoints.V2.RunCodeAction);
                var runRequest = new RunCodeActionRequest
                {
                    Line = point.Line,
                    Column = point.Offset,
                    FileName = testFile.FileName,
                    Identifier = $"Change namespace to '{expectedNamespace}'",
                    WantsTextChanges = false,
                    WantsAllCodeActionOperations = true,
                    Buffer = testFile.Content.Code
                };
                var runResponse = await runRequestHandler.Handle(runRequest);

                AssertIgnoringIndent(expected, ((ModifiedFileResponse)runResponse.Changes.First()).Buffer);
            }
        }

        [Theory]
        [InlineData("LiveChanged", "Bar.cs")]
        [InlineData("LiveChanged.Foo", "Foo", "Bar.cs")]
        [InlineData("LiveChanged.Foo.Bar", "Foo", "Bar", "Baz.cs")]
        public async Task RespectFolderName_InExecutedCodeActions_AfterLiveChange(string expectedNamespace, params string[] relativePath)
        {
            var expected = "namespace " + expectedNamespace + " { }";
            var testFile = new TestFile(Path.Combine(TestAssets.Instance.TestFilesFolder, Path.Combine(relativePath)), @"namespace Xx$$x { }");

            using (var host = CreateOmniSharpHost(new[] { testFile }, null, TestAssets.Instance.TestFilesFolder))
            {
                var point = testFile.Content.GetPointFromPosition();
                var runRequestHandler = host.GetRequestHandler<RunCodeActionService>(OmniSharpEndpoints.V2.RunCodeAction);

                var changedSolution = host.Workspace.CurrentSolution.WithProjectDefaultNamespace(host.Workspace.CurrentSolution.Projects.ElementAt(0).Id, "LiveChanged");
                host.Workspace.TryApplyChanges(changedSolution);

                var runRequest = new RunCodeActionRequest
                {
                    Line = point.Line,
                    Column = point.Offset,
                    FileName = testFile.FileName,
                    Identifier = $"Change namespace to '{expectedNamespace}'",
                    WantsTextChanges = false,
                    WantsAllCodeActionOperations = true,
                    Buffer = testFile.Content.Code
                };
                var runResponse = await runRequestHandler.Handle(runRequest);

                AssertIgnoringIndent(expected, ((ModifiedFileResponse)runResponse.Changes.First()).Buffer);
            }
        }

        [Fact]
        public void CheckIfOnDefaultNamespaceChangedIsAvailableInRoslyn()
        {
            // This tracks the availability of OnDefaultNamespaceChanged on the Workspace
            // at the moment it's not and we need to manually sync default namespace after it changes
            // when OmniSharp is running by having reflection in
            // protected override void ApplyProjectChanges(ProjectChanges projectChanges)
            // once it's fixed in Roslyn we can get rid of this test
            var onDefaultNamespaceChanged = typeof(OmniSharpWorkspace).GetMethod("OnDefaultNamespaceChanged", BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.NotNull(onDefaultNamespaceChanged);

            var parameters = onDefaultNamespaceChanged.GetParameters();
            Assert.Equal(2, parameters.Count());
            Assert.Equal(typeof(ProjectId), parameters[0].ParameterType);
            Assert.Equal(typeof(string), parameters[1].ParameterType);
        }

        private static void AssertIgnoringIndent(string expected, string actual)
        {
            Assert.Equal(TrimLines(expected), TrimLines(actual), false, true, true);
        }

        private static string TrimLines(string source)
        {
            return string.Join("\n", source.Split('\n').Select(s => s.Trim()));
        }
    }
}
