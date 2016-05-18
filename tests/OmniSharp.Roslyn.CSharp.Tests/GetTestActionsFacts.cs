using System.IO;
using System.Linq;
using OmniSharp.Models.V2;
using OmniSharp.Roslyn.CSharp.Services.Testing;
using OmniSharp.Roslyn.CSharp.Tests.Utility;
using TestCommon;
using Xunit;


namespace OmniSharp.Roslyn.CSharp.Tests
{
    public class GetTestActionsFacts
    {
        [Fact]
        public void ReturnsOneTestAction()
        {
            var sampleProject = TestsContext.Default.GetTestSample("BasicTestProjectSample01");
            var workspace = WorkspaceHelper.Create(sampleProject).FirstOrDefault();
            Assert.NotNull(workspace);
            
            var request = new GetCodeActionsRequest()
            {
                FileName = Path.Combine(sampleProject, "TestProgram.cs"),
                Selection = new Range { Start = new Point { Line = 7, Column = 22 }, End = new Point { Line = 7, Column = 22 } }
            };

            var actions = TestActionsProvider.FindTestActions(workspace, request);
            
            Assert.NotEmpty(actions);
        }
    }
}