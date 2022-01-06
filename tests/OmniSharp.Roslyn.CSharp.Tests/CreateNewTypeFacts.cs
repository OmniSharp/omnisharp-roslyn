using System.IO;
using System.Threading.Tasks;
using System.Linq;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Text;
using OmniSharp.Models.CreateNewTypeRequest;
using OmniSharp.Roslyn.CSharp.Services.CreateNewType;
using TestUtility;
using Xunit;
using Xunit.Abstractions;

namespace OmniSharp.Roslyn.CSharp.Tests
{
    public class CreateNewTypeFacts : AbstractSingleRequestHandlerTestFixture<CreateNewTypeService>
    {
        public CreateNewTypeFacts(ITestOutputHelper testOutput, SharedOmniSharpHostFixture sharedOmniSharpHostFixture)
            : base(testOutput, sharedOmniSharpHostFixture)
        {
        }

        protected override string EndpointName => OmniSharpEndpoints.CreateNewType;

        [Fact]
        public async Task CreateNewType_Class_FileWithClassCreated()
        {
            string testProjectName = "ProjectAndSolution";
            using ITestProject testProject = await TestAssets.Instance.GetTestProjectAsync(testProjectName);
            using OmniSharpTestHost host = CreateOmniSharpHost(testProject.Directory);
            TestHelpers.AddProjectToWorkspace(host.Workspace, Path.Combine(testProject.Directory, $"{testProjectName}.csproj"), new[] {"net472"}, new TestFile[] { });
            CreateNewTypeService requestHandler = GetRequestHandler(host);
            CreateNewTypeRequest request = new()
            {
                FileParentPath = $"{testProject.Directory}",
                Type = TypeEnum.Class,
                SymbolName = "Lol"
            };
            string expectedFileName = "Lol.cs";
            string expectedSourceText = @"
namespace OmniSharpTest;

public class Lol
{
}";

            await requestHandler.Handle(request);
            
            Solution solution = host.Workspace.CurrentSolution;
            DocumentId documentId = solution.GetDocumentIdsWithFilePath(Path.Combine(testProject.Directory, expectedFileName)).First();
            Document document = solution.GetDocument(documentId);
            SourceText sourceText = await document.GetTextAsync();
            Assert.Equal(expectedSourceText.Trim('\r', '\n'), sourceText.ToString());
        }

        [Fact]
        public async Task CreateNewType_Interface_FileWithInterfaceCreated()
        {
            string testProjectName = "ProjectAndSolution";
            using ITestProject testProject = await TestAssets.Instance.GetTestProjectAsync(testProjectName);
            using OmniSharpTestHost host = CreateOmniSharpHost(testProject.Directory);
            TestHelpers.AddProjectToWorkspace(host.Workspace, Path.Combine(testProject.Directory, $"{testProjectName}.csproj"), new[] {"net472"}, new TestFile[] { });
            CreateNewTypeService requestHandler = GetRequestHandler(host);
            CreateNewTypeRequest request = new()
            {
                FileParentPath = $"{testProject.Directory}",
                Type = TypeEnum.Interface,
                SymbolName = "Lol"
            };
            string expectedFileName = "Lol.cs";
            string expectedSourceText = @"
namespace OmniSharpTest;

public interface Lol
{
}
";

            await requestHandler.Handle(request);
            
            Solution solution = host.Workspace.CurrentSolution;
            DocumentId documentId = solution.GetDocumentIdsWithFilePath(Path.Combine(testProject.Directory, expectedFileName)).First();
            Document document = solution.GetDocument(documentId);
            SourceText sourceText = await document.GetTextAsync();
            Assert.Equal(expectedSourceText.Trim('\r', '\n'), sourceText.ToString());
        }
    }
}