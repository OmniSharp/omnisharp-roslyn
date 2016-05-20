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
    public class XunitTestActionRunnerFacts
    {
        private readonly TestMethodsDiscover _provider;
        private readonly ILoggerFactory _loggerFactory;

        public XunitTestActionRunnerFacts()
        {
            _loggerFactory = new LoggerFactory();
            _loggerFactory.AddConsole();

            _provider = new TestMethodsDiscover(_loggerFactory);
        }

        [Fact]
        public async Task GetProcessStartInfoFromDotnetTest()
        {
            ILogger logger = _loggerFactory.CreateLogger($"{typeof(XunitTestActionRunnerFacts).FullName}.{nameof(GetProcessStartInfoFromDotnetTest)}");
            string sampleProject = TestsContext.Default.GetTestSample("BasicTestProjectSample01");

            ITestActionRunner runner = _provider.GetTestActionRunner(new RunCodeActionRequest
            {
                FileName = Path.Combine(sampleProject, "TestProgram.cs"),
                Identifier = "test.debug|Main.Test.MainTest.Test"
            });
            
            logger.LogInformation($"Created runner: {runner}");

            var result = await runner.RunAsync();
            Assert.False(result.ToRespnse().TestResult);
            Assert.NotEmpty(result.ToRespnse().DebugTestCommand);
        }
    }
}