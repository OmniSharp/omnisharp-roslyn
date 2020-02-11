using System.IO;
using System.Linq;
using System.Threading.Tasks;
using OmniSharp.DotNetTest.Models;
using OmniSharp.DotNetTest.Services;
using TestUtility;
using Xunit;
using Xunit.Abstractions;

namespace OmniSharp.DotNetTest.Tests
{
    public abstract class AbstractDiscoverTestFacts : AbstractTestFixture
    {
        public AbstractDiscoverTestFacts(ITestOutputHelper testOutput)
            : base(testOutput)
        {
        }

        internal DiscoverTestsService GetRequestHandler(OmniSharpTestHost host)
        {
            return host.GetRequestHandler<DiscoverTestsService>(OmniSharpEndpoints.V2.DiscoverTests);
        }

        protected async Task DiscoverTestsAsync(string projectName, string testFramework, string targetFrameworkVersion, Test expectedTest)
        {
            using (var testProject = await TestAssets.Instance.GetTestProjectAsync(projectName))
            using (var host = CreateOmniSharpHost(testProject.Directory, null, DotNetCliVersion))
            {
                var service = GetRequestHandler(host);

                var request = new DiscoverTestsRequest
                {
                    FileName = Path.Combine(testProject.Directory, "TestProgram.cs"),
                    TestFrameworkName = testFramework,
                    TargetFrameworkVersion = targetFrameworkVersion
                };

                var response = await service.Handle(request);

                if (expectedTest == null)
                {
                    Assert.True(response.Tests?.Length == 0, "Expected no tests to be discovered.");
                }
                else
                {
                    var test = response.Tests.SingleOrDefault(o => o.FullyQualifiedName == expectedTest.FullyQualifiedName);
                    Assert.True(test != null, "Expected test with matching FullyQualifiedName");
                    Assert.True(test.DisplayName == expectedTest.DisplayName, "Expected DisplayName to match");
                    Assert.True(test.LineNumber == expectedTest.LineNumber, "Expected LineNumber to match");
                }
            }
        }
    }
}
