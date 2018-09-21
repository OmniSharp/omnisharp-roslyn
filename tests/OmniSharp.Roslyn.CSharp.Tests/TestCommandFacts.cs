using OmniSharp.ConfigurationManager;
using OmniSharp.Models;
using OmniSharp.Models.TestCommand;
using OmniSharp.Roslyn.CSharp.Services.TestCommands;
using OmniSharp.Services;
using System.Linq;
using System.Threading.Tasks;
using TestUtility;
using Xunit;
using Xunit.Abstractions;

namespace OmniSharp.Roslyn.CSharp.Tests
{
    public class TestCommandFacts : AbstractSingleRequestHandlerTestFixture<TestCommandService>
    {
        protected override string EndpointName => OmniSharpEndpoints.TestCommand;

        public TestCommandFacts(ITestOutputHelper output, SharedOmniSharpHostFixture sharedOmniSharpHostFixture)
            :base(output,sharedOmniSharpHostFixture)
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
        public async Task GetTestCommand_Fixture_Test()
        {
            const string source = @"
using NUnit.Framework;
using Should;

namespace TestApp.Tests
{
    [TestFixture]
    public class T$$ests
    {
        [Test]
        public void Should_be_true()
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
                    Path = "path\\to\\source"
                },
                TestCommands = new TestCommands()
                {
                    All = "nunit3-console.exe --noresult --noh {{AssemblyPath}}",
                    Single = "nunit3-console.exe --noresult --noh {{AssemblyPath}} --test={{TypeName}}",
                    Fixture = "nunit3-console.exe --noresult --noh {{AssemblyPath}} --test={{TypeName}}"
                }
            };

            var runTest = await GetTestCommandAsync(source, TestCommandType.Fixture);
            Assert.NotNull(runTest);
            Assert.Contains("TestApp.Tests.Tests", runTest);
        
        }

        [Fact]
        public async Task GetTestCommand_All_Test()
        {
            const string source = @"
using NUnit.Framework;
using Should;

namespace TestApp.T$$ests
{
    [TestFixture]
    public class Tests
    {
        [Test]
        public void Should_be_true()
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
                    Path = "path\\to\\source"
                },
                TestCommands = new TestCommands()
                {
                    All = "nunit3-console.exe --noresult --noh {{AssemblyPath}}",
                    Single = "nunit3-console.exe --noresult --noh {{AssemblyPath}} --test={{TypeName}}",
                    Fixture = "nunit3-console.exe --noresult --noh {{AssemblyPath}} --test={{TypeName}}"
                }
            };

            var runTest = await GetTestCommandAsync(source, TestCommandType.All);
            Assert.NotNull(runTest);
            Assert.Contains("nunit3-console.exe --noresult --noh", runTest);
        }

        [Fact]
        public async Task GetTestCommand_Single_Test()
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
                    Path = "path\\to\\source"
                },
                TestCommands = new TestCommands()
                {
                    All = "nunit3-console.exe --noresult --noh {{AssemblyPath}}",
                    Single = "nunit3-console.exe --noresult --noh {{AssemblyPath}} --test={{TypeName}}",
                    Fixture = "nunit3-console.exe --noresult --noh {{AssemblyPath}} --test={{TypeName}}"
                }
            };

            var runTest = await GetTestCommandAsync(source, TestCommandType.Single);
            Assert.NotNull(runTest);
            Assert.Contains( "Tests.Tests.Should_be_true", runTest);
        }

        private async Task<string> GetTestCommandAsync(string source, TestCommandType testType)
        {
            var testFile = new TestFile("dummy.cs", source);
            SharedOmniSharpTestHost.AddFilesToWorkspace(testFile);

            var rh = SharedOmniSharpTestHost.GetRequestHandler<TestCommandService>(OmniSharpEndpoints.TestCommand);
            OmniSharpConfiguration config = new OmniSharpConfiguration()
            {
                MSBuildPath = new BuildPath()
                {
                    Path = "path\\to\\source"
                },
                TestCommands = new TestCommands()
                {
                    All = "nunit3-console.exe --noresult --noh {{AssemblyPath}}",
                    Single = "nunit3-console.exe --noresult --noh {{AssemblyPath}} --test={{TypeName}}",
                    Fixture = "nunit3-console.exe --noresult --noh {{AssemblyPath}} --test={{TypeName}}"
                }
            };
            rh._config = config;
            var cc = rh._config;
            rh._testCommandProviders.First().testCommands = TestCommandType.All;

            var point = testFile.Content.GetPointFromPosition();
            var requestHandler = GetRequestHandler(SharedOmniSharpTestHost);
            var request = new TestCommandRequest
            {
                Type = testType,
                Line = point.Line,
                FileName = testFile.FileName,
                Buffer = testFile.Content.Code
            };
            var response = await requestHandler.Handle(request);
            return response.TestCommand;
        }
    }
}
