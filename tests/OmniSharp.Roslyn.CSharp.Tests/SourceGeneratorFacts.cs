using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Diagnostics;
using OmniSharp.Models.v1.SourceGeneratedFile;
using OmniSharp.Models.V2.GotoDefinition;
using OmniSharp.Roslyn.CSharp.Services.Buffer;
using OmniSharp.Roslyn.CSharp.Services.Navigation;
using System.Collections.Immutable;
using System.Linq;
using System.Threading.Tasks;
using TestUtility;
using Xunit;
using Xunit.Abstractions;

namespace OmniSharp.Roslyn.CSharp.Tests
{
    public class SourceGeneratorFacts : AbstractTestFixture
    {
        public SourceGeneratorFacts(ITestOutputHelper output, SharedOmniSharpHostFixture sharedOmniSharpHostFixture) : base(output, sharedOmniSharpHostFixture)
        {
        }

        [Fact]
        public async Task UpdateReturnsChanges()
        {
            const string Code = @"
_ = GeneratedCode.$$S;
_ = ""Hello world!""";
            const string Path = @"Test.cs";
            var reference = new TestGeneratorReference(context =>
            {
                // NOTE: Don't actually do this in a real generator. This is just for test
                // code. Do not use this as an example of what to do in production.

                var syntax = context.Compilation.SyntaxTrees.Single().GetRoot().DescendantNodes().OfType<LiteralExpressionSyntax>().SingleOrDefault();
                if (syntax != null)
                {
                    context.AddSource("GeneratedSource", @"
class GeneratedCode
{
    public static string S = " + syntax.ToString() + @";
}");
                }
            });

            TestFile testFile = new TestFile(Path, Code);
            TestHelpers.AddProjectToWorkspace(SharedOmniSharpTestHost.Workspace,
                "project.csproj",
                new[] { "netcoreapp3.1" },
                new[] { testFile },
                ImmutableArray.Create<AnalyzerReference>(reference));

            var point = testFile.Content.GetPointFromPosition();

            var gotoDefRequest = new GotoDefinitionRequest { FileName = Path, Line = point.Line, Column = point.Offset };
            var gotoDefResponse = (await SharedOmniSharpTestHost
                .GetRequestHandler<GotoDefinitionServiceV2>(OmniSharpEndpoints.V2.GotoDefinition)
                .Handle(gotoDefRequest)).Definitions.Single();

            var initialContent = await SharedOmniSharpTestHost
                .GetRequestHandler<SourceGeneratedFileService>(OmniSharpEndpoints.SourceGeneratedFile)
                .Handle(new SourceGeneratedFileRequest()
                {
                    ProjectGuid = gotoDefResponse.SourceGeneratedFileInfo.ProjectGuid,
                    DocumentGuid = gotoDefResponse.SourceGeneratedFileInfo.DocumentGuid
                });

            Assert.Contains("Hello world!", initialContent.Source);

            var updateRequest = new UpdateSourceGeneratedFileRequest
            {
                DocumentGuid = gotoDefResponse.SourceGeneratedFileInfo.DocumentGuid,
                ProjectGuid = gotoDefResponse.SourceGeneratedFileInfo.ProjectGuid
            };
            var updateHandler = SharedOmniSharpTestHost.GetRequestHandler<SourceGeneratedFileService>(OmniSharpEndpoints.UpdateSourceGeneratedFile);
            var updatedResponse = await updateHandler.Handle(updateRequest);
            Assert.Null(updatedResponse.Source);
            Assert.Equal(UpdateType.Unchanged, updatedResponse.UpdateType);

            var updateBufferHandler = SharedOmniSharpTestHost.GetRequestHandler<UpdateBufferService>(OmniSharpEndpoints.UpdateBuffer);
            _ = await updateBufferHandler.Handle(new()
            {
                FileName = Path,
                Buffer = Code.Replace("Hello world!", "Goodbye!")
            });

            updatedResponse = await updateHandler.Handle(updateRequest);
            Assert.Equal(UpdateType.Modified, updatedResponse.UpdateType);
            Assert.Contains("Goodbye!", updatedResponse.Source);
            Assert.DoesNotContain("Hello world!", updatedResponse.Source);

            _ = await updateBufferHandler.Handle(new()
            {
                FileName = Path,
                Buffer = @"_ = GeneratedCode.S;"
            });

            updatedResponse = await updateHandler.Handle(updateRequest);
            Assert.Equal(UpdateType.Deleted, updatedResponse.UpdateType);
            Assert.Null(updatedResponse.Source);
        }
    }
}
