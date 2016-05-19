using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using OmniSharp.Models.V2;
using OmniSharp.Roslyn.CSharp.Services.Testing;
using OmniSharp.Roslyn.CSharp.Tests.Utility;
using TestCommon;
using Xunit;


namespace OmniSharp.Roslyn.CSharp.Tests
{
    public class GetTestActionsFacts
    {
        private readonly TestMethodsDiscover _provider;
        private readonly ILoggerFactory _loggerFactory;

        public GetTestActionsFacts()
        {
            _loggerFactory = new LoggerFactory();
            _loggerFactory.AddConsole();

            _provider = new TestMethodsDiscover(_loggerFactory);
        }

        [Fact]
        public async Task FoundFactsBasedTest()
        {
            var sampleProject = TestsContext.Default.GetTestSample("BasicTestProjectSample01");
            var workspace = WorkspaceHelper.Create(sampleProject).FirstOrDefault();
            Assert.NotNull(workspace);

            var request = new GetCodeActionsRequest()
            {
                FileName = Path.Combine(sampleProject, "TestProgram.cs"),
                Selection = new Range { Start = new Point { Line = 7, Column = 22 }, End = new Point { Line = 7, Column = 22 } }
            };

            var testMethods = await _provider.FindTestActions(workspace, request);
            Assert.Equal("Main.Test.MainTest.Test", testMethods.Single());
        }

        [Fact]
        public async Task FoundTheoryBasedTest()
        {
            var sampleProject = TestsContext.Default.GetTestSample("BasicTestProjectSample01");
            var workspace = WorkspaceHelper.Create(sampleProject).FirstOrDefault();
            Assert.NotNull(workspace);

            var request = new GetCodeActionsRequest()
            {
                FileName = Path.Combine(sampleProject, "TestProgram.cs"),
                Selection = new Range { Start = new Point { Line = 15, Column = 26 }, End = new Point { Line = 15, Column = 26 } }
            };

            var testMethods = await _provider.FindTestActions(workspace, request);
            Assert.Equal("Main.Test.MainTest.DataDrivenTest", testMethods.Single());
        }

        [Fact]
        public async Task NotFoundTestMethod()
        {
            var sampleProject = TestsContext.Default.GetTestSample("BasicTestProjectSample01");
            var workspace = WorkspaceHelper.Create(sampleProject).FirstOrDefault();
            Assert.NotNull(workspace);

            var request = new GetCodeActionsRequest()
            {
                FileName = Path.Combine(sampleProject, "TestProgram.cs"),
                Selection = new Range { Start = new Point { Line = 20, Column = 27 }, End = new Point { Line = 20, Column = 27 } }
            };

            var testMethods = await _provider.FindTestActions(workspace, request);
            Assert.Empty(testMethods);
        }
    }
}