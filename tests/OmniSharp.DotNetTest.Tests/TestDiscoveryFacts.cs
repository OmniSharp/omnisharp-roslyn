using System.IO;
using System.Linq;
using System.Threading.Tasks;
using OmniSharp.Models.MembersTree;
using OmniSharp.Roslyn.CSharp.Services.Structure;
using TestUtility;
using Xunit;
using Xunit.Abstractions;

namespace OmniSharp.DotNetTest.Tests
{
    public class TestDiscoveryFacts : AbstractTestFixture
    {
        private readonly TestAssets _testAssets;

        public TestDiscoveryFacts(ITestOutputHelper output)
            : base(output)
        {
            this._testAssets = TestAssets.Instance;
        }

        [Theory]
        [InlineData("XunitTestProject", "TestProgram.cs", 8, 20, true, "XunitTestMethod", "Main.Test.MainTest.Test")]
        [InlineData("XunitTestProject", "TestProgram.cs", 16, 20, true, "XunitTestMethod", "Main.Test.MainTest.DataDrivenTest1")]
        [InlineData("XunitTestProject", "TestProgram.cs", 24, 20, true, "XunitTestMethod", "Main.Test.MainTest.DataDrivenTest2")]
        [InlineData("XunitTestProject", "TestProgram.cs", 53, 20, true, "XunitTestMethod", "Main.Test.MainTest.FailingTest")]
        [InlineData("XunitTestProject", "TestProgram.cs", 59, 20, true, "XunitTestMethod", "Main.Test.MainTest.CheckStandardOutput")]
        [InlineData("XunitTestProject", "TestProgram.cs", 29, 21, false, "", "")]
        [InlineData("NUnitTestProject", "TestProgram.cs", 8, 20, true, "NUnitTestMethod", "Main.Test.MainTest.Test")]
        [InlineData("NUnitTestProject", "TestProgram.cs", 15, 20, true, "NUnitTestMethod", "Main.Test.MainTest.DataDrivenTest1")]
        [InlineData("NUnitTestProject", "TestProgram.cs", 22, 20, true, "NUnitTestMethod", "Main.Test.MainTest.DataDrivenTest2")]
        [InlineData("NUnitTestProject", "TestProgram.cs", 28, 20, true, "NUnitTestMethod", "Main.Test.MainTest.SourceDataDrivenTest")]
        [InlineData("NUnitTestProject", "TestProgram.cs", 34, 20, true, "NUnitTestMethod", "Main.Test.MainTest.FailingTest")]
        [InlineData("NUnitTestProject", "TestProgram.cs", 47, 20, false, "", "")]
        public async Task FindTestMethods(string projectName, string fileName, int line, int column, bool found, string expectedFeatureName, string expectedMethodName)
        {
            using (var testProject = await this._testAssets.GetTestProjectAsync(projectName))
            using (var host = CreateOmniSharpHost(testProject.Directory))
            {
                var filePath = Path.Combine(testProject.Directory, fileName);

                var containingMember = await GetContainingMemberAsync(host, filePath, line, column);

                if (found)
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
        }

        // TODO: NUnit tests are disabled for now because they are failing on Linux (but not Windows or OSX).
        // From what I can tell, the nunit assemblies aren't added as metadata references. I *suspect* there's
        // some sort of path case-sensitivity issue.

        [ConditionalTheory(typeof(IsLegacyTest))]
        [InlineData("LegacyXunitTestProject", "TestProgram.cs", 7, 20, true, "XunitTestMethod", "Main.Test.MainTest.Test")]
        [InlineData("LegacyXunitTestProject", "TestProgram.cs", 15, 20, true, "XunitTestMethod", "Main.Test.MainTest.DataDrivenTest1")]
        [InlineData("LegacyXunitTestProject", "TestProgram.cs", 23, 20, true, "XunitTestMethod", "Main.Test.MainTest.DataDrivenTest2")]
        [InlineData("LegacyXunitTestProject", "TestProgram.cs", 28, 21, false, "", "")]
        //[InlineData("LegacyNUnitTestProject", "TestProgram.cs", 7, 20, true, "NUnitTestMethod", "Main.Test.MainTest.Test")]
        //[InlineData("LegacyNUnitTestProject", "TestProgram.cs", 14, 20, true, "NUnitTestMethod", "Main.Test.MainTest.DataDrivenTest1")]
        //[InlineData("LegacyNUnitTestProject", "TestProgram.cs", 21, 20, true, "NUnitTestMethod", "Main.Test.MainTest.DataDrivenTest2")]
        //[InlineData("LegacyNUnitTestProject", "TestProgram.cs", 27, 20, true, "NUnitTestMethod", "Main.Test.MainTest.SourceDataDrivenTest")]
        //[InlineData("LegacyNUnitTestProject", "TestProgram.cs", 32, 20, false, "", "")]
        public async Task LegacyFindTestMethods(string projectName, string fileName, int line, int column, bool found, string expectedFeatureName, string expectedMethodName)
        {
            using (var testProject = await this._testAssets.GetTestProjectAsync(projectName, legacyProject: true))
            using (var host = CreateOmniSharpHost(testProject.Directory))
            {
                var filePath = Path.Combine(testProject.Directory, fileName);

                var containingMember = await GetContainingMemberAsync(host, filePath, line, column);

                if (found)
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
    }
}
