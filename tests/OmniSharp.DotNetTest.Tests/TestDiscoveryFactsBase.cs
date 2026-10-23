using System.IO;
using System.Linq;
using System.Threading.Tasks;
using OmniSharp.Models.MembersTree;
using OmniSharp.Models.V2;
using OmniSharp.Models.V2.CodeStructure;
using OmniSharp.Roslyn.CSharp.Services.Structure;
using TestUtility;
using Xunit;

namespace OmniSharp.DotNetTest.Tests
{
    public abstract class TestDiscoveryFactsBase : AbstractTestFixture
    {
        protected const string xunit = nameof(xunit);
        protected const string XunitTestMethod = "XunitTestMethod";

        protected const string nunit = nameof(nunit);
        protected const string NUnitTestMethod = "NUnitTestMethod";

        protected const string TestProgram = "TestProgram.cs";

        private readonly TestAssets _testAssets;

        protected TestDiscoveryFactsBase(ITestOutputHelper output)
            : base(output)
        {
            this._testAssets = TestAssets.Instance;
        }

        public override DotNetCliVersion DotNetCliVersion { get; } = 0;

        protected async Task AssertTestMethodAsync(string projectName, string fileName, int line, int column, bool expectToFind, string expectedTestFramework, string expectedFeatureName, string expectedMethodName)
        {
            using (var testProject = await this._testAssets.GetTestProjectAsync(projectName))
            using (var host = CreateOmniSharpHost(testProject.Directory, null, DotNetCliVersion.Current))
            {
                var filePath = Path.Combine(testProject.Directory, fileName);

                await AssertWithMemberTree(host, filePath, line, column, expectToFind, expectedFeatureName, expectedMethodName);
                await AssertWithCodeStructure(host, filePath, line, column, expectToFind, expectedTestFramework, expectedMethodName);
            }
        }

        private async Task AssertWithMemberTree(OmniSharpTestHost host, string filePath, int line, int column, bool expectToFind, string expectedFeatureName, string expectedMethodName)
        {
            var containingMember = await GetContainingMemberAsync(host, filePath, line, column);

            if (expectToFind)
            {
                var feature = containingMember.Features.Single();
                Assert.Equal(expectedFeatureName, feature.Name);
                Assert.Equal(expectedMethodName, feature.Data);
            }
            else
            {
                Assert.Empty(containingMember.Features);
            }
        }

        private static async Task<FileMemberElement> GetContainingMemberAsync(OmniSharpTestHost host, string filePath, int line, int column)
        {
            var membersAsTreeService = host.GetRequestHandler<MembersAsTreeService>(OmniSharpEndpoints.MembersTree);

            var request = new MembersTreeRequest
            {
                FileName = filePath
            };

            var response = await membersAsTreeService.Handle(request);

            FileMemberElement containingMember = null;

            foreach (var node in response.TopLevelTypeDefinitions)
            {
                foreach (var child in node.ChildNodes)
                {
                    if (child.Location.Contains(line, column))
                    {
                        Assert.Null(containingMember);
                        containingMember = child;
                    }
                }
            }

            Assert.NotNull(containingMember);

            return containingMember;
        }

        private async Task AssertWithCodeStructure(OmniSharpTestHost host, string filePath, int line, int column, bool expectToFind, string expectedTestFramework, string expectedTestMethodName)
        {
            var codeElement = await GetContainingCodeElementAsync(host, filePath, line, column);

            if (expectToFind)
            {
                if (codeElement.Properties.TryGetValue("testFramework", out var testFramework) &&
                    codeElement.Properties.TryGetValue("testMethodName", out var testMethodName))
                {
                    Assert.Equal(expectedTestFramework, testFramework);
                    Assert.Equal(expectedTestMethodName, testMethodName);
                }
                else
                {
                    Assert.Fail("Did not find test method.");
                }
            }
        }

        private static async Task<CodeElement> GetContainingCodeElementAsync(OmniSharpTestHost host, string filePath, int line, int column)
        {
            var codeStructureService = host.GetRequestHandler<CodeStructureService>(OmniSharpEndpoints.V2.CodeStructure);

            var request = new CodeStructureRequest
            {
                FileName = filePath
            };

            var response = await codeStructureService.Handle(request);

            CodeElement result = null;
            var elements = response.Elements;

            while (elements != null)
            {
                var stop = true;

                foreach (var element in elements)
                {
                    if (element.Ranges[SymbolRangeNames.Full].Contains(line, column))
                    {
                        result = element;
                        elements = element.Children;
                        stop = false;
                        break;
                    }
                }

                if (stop)
                {
                    break;
                }
            }

            Assert.NotNull(result);

            return result;
        }
    }
}
