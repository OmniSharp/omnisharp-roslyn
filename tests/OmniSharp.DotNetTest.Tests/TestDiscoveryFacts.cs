using System.IO;
using System.Linq;
using System.Threading.Tasks;
using OmniSharp.Models.MembersTree;
using OmniSharp.Models.V2.CodeStructure;
using OmniSharp.Roslyn.CSharp.Services.Structure;
using TestUtility;
using Xunit;
using Xunit.Abstractions;

namespace OmniSharp.DotNetTest.Tests
{
    public class TestDiscoveryFacts : AbstractTestFixture
    {
        private const string xunit = nameof(xunit);
        private const string XunitTestMethod = "XunitTestMethod";

        private const string nunit = nameof(nunit);
        private const string NUnitTestMethod = "NUnitTestMethod";

        private const string TestProgram = "TestProgram.cs";

        private readonly TestAssets _testAssets;

        public TestDiscoveryFacts(ITestOutputHelper output)
            : base(output)
        {
            this._testAssets = TestAssets.Instance;
        }

        public override DotNetCliVersion DotNetCliVersion { get; } = 0;

        [Theory]
        [InlineData(XunitTestProject, TestProgram, 8, 20, true, xunit, XunitTestMethod, "Main.Test.MainTest.Test")]
        [InlineData(XunitTestProject, TestProgram, 16, 20, true, xunit, XunitTestMethod, "Main.Test.MainTest.DataDrivenTest1")]
        [InlineData(XunitTestProject, TestProgram, 24, 20, true, xunit, XunitTestMethod, "Main.Test.MainTest.DataDrivenTest2")]
        [InlineData(XunitTestProject, TestProgram, 53, 20, true, xunit, XunitTestMethod, "Main.Test.MainTest.FailingTest")]
        [InlineData(XunitTestProject, TestProgram, 59, 20, true, xunit, XunitTestMethod, "Main.Test.MainTest.CheckStandardOutput")]
        [InlineData(XunitTestProject, TestProgram, 29, 21, false, "", "", "")]
        [InlineData(NUnitTestProject, TestProgram, 8, 20, true, nunit, NUnitTestMethod, "Main.Test.MainTest.Test")]
        [InlineData(NUnitTestProject, TestProgram, 15, 20, true, nunit, NUnitTestMethod, "Main.Test.MainTest.DataDrivenTest1")]
        [InlineData(NUnitTestProject, TestProgram, 22, 20, true, nunit, NUnitTestMethod, "Main.Test.MainTest.DataDrivenTest2")]
        [InlineData(NUnitTestProject, TestProgram, 28, 20, true, nunit, NUnitTestMethod, "Main.Test.MainTest.SourceDataDrivenTest")]
        [InlineData(NUnitTestProject, TestProgram, 34, 20, true, nunit, NUnitTestMethod, "Main.Test.MainTest.FailingTest")]
        [InlineData(NUnitTestProject, TestProgram, 47, 20, false, "", "", "")]
        public async Task FindTestMethods(string projectName, string fileName, int line, int column, bool expectToFind, string expectedTestFramework, string expectedFeatureName, string expectedMethodName)
        {
            using (var testProject = await this._testAssets.GetTestProjectAsync(projectName))
            using (var host = CreateOmniSharpHost(testProject.Directory, ConfigurationData, DotNetCliVersion.Current))
            {
                var filePath = Path.Combine(testProject.Directory, fileName);

                await AssertWithMemberTree(host, filePath, line, column, expectToFind, expectedFeatureName, expectedMethodName);
                await AssertWithCodeStructure(host, filePath, line, column, expectToFind, expectedTestFramework, expectedMethodName);
            }
        }

        [ConditionalTheory(typeof(IsLegacyTest))]
        [InlineData(LegacyXunitTestProject, TestProgram, 7, 20, true, xunit, XunitTestMethod, "Main.Test.MainTest.Test")]
        [InlineData(LegacyXunitTestProject, TestProgram, 15, 20, true, xunit, XunitTestMethod, "Main.Test.MainTest.DataDrivenTest1")]
        [InlineData(LegacyXunitTestProject, TestProgram, 23, 20, true, xunit, XunitTestMethod, "Main.Test.MainTest.DataDrivenTest2")]
        [InlineData(LegacyXunitTestProject, TestProgram, 28, 21, false, "", "", "")]
        [InlineData(LegacyNUnitTestProject, TestProgram, 7, 20, true, nunit, NUnitTestMethod, "Main.Test.MainTest.Test")]
        [InlineData(LegacyNUnitTestProject, TestProgram, 14, 20, true, nunit, NUnitTestMethod, "Main.Test.MainTest.DataDrivenTest1")]
        [InlineData(LegacyNUnitTestProject, TestProgram, 21, 20, true, nunit, NUnitTestMethod, "Main.Test.MainTest.DataDrivenTest2")]
        [InlineData(LegacyNUnitTestProject, TestProgram, 27, 20, true, nunit, NUnitTestMethod, "Main.Test.MainTest.SourceDataDrivenTest")]
        [InlineData(LegacyNUnitTestProject, TestProgram, 32, 20, false, "", "", "")]
        public async Task LegacyFindTestMethods(string projectName, string fileName, int line, int column, bool expectToFind, string expectedTestFramework, string expectedFeatureName, string expectedMethodName)
        {
            using (var testProject = await this._testAssets.GetTestProjectAsync(projectName, legacyProject: true))
            using (var host = CreateOmniSharpHost(testProject.Directory, ConfigurationData, DotNetCliVersion.Legacy))
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
                    Assert.True(false, "Did not find test method.");
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
                foreach (var element in elements)
                {
                    if (element.Ranges[CodeElementRangeNames.Full].Contains(line, column))
                    {
                        result = element;
                        elements = element.Children;
                        break;
                    }
                }
            }

            Assert.NotNull(result);

            return result;
        }
    }
}
