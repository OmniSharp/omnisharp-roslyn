using Microsoft.Build.Framework;
using OmniSharp.MSBuild.Logging;
using OmniSharp.Utilities;
using Xunit;
using Xunit.Abstractions;

namespace OmniSharp.MSBuild.Tests
{
    public class MSBuildDiagnosticTests : AbstractMSBuildTestFixture
    {
        public MSBuildDiagnosticTests(ITestOutputHelper output) : base(output)
        {
        }

        [Fact]
        public void MSB3644_CustomMessage()
        {
            var sourceDiagnostic = new BuildErrorEventArgs("test-subcategory", "MSB3644", "foo.cs", 1, 1, 1, 1, "Reference assemblies not found!", "help-keyword", "dummy-sender");
            var msbuildDiagnostic = MSBuildDiagnostic.CreateFrom(sourceDiagnostic);

            Assert.Equal(Platform.Current.OperatingSystem != OperatingSystem.Windows
                ? ErrorMessages.ReferenceAssembliesNotFoundUnix : sourceDiagnostic.Message, msbuildDiagnostic.Message);
        }
    }
}
