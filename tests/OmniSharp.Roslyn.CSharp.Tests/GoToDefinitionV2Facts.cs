using System.Threading.Tasks;
using OmniSharp.Roslyn.CSharp.Services.Navigation;
using OmniSharp.Models.Metadata;
using TestUtility;
using Xunit;
using Xunit.Abstractions;
using System.Collections.Generic;
using OmniSharp.Models.V2.GotoDefinition;
using OmniSharp.Models.v1.SourceGeneratedFile;

namespace OmniSharp.Roslyn.CSharp.Tests
{
    public class GoToDefinitionV2Facts : AbstractGoToDefinitionFacts<GotoDefinitionServiceV2, GotoDefinitionRequest, GotoDefinitionResponse>
    {
        public GoToDefinitionV2Facts(ITestOutputHelper output, SharedOmniSharpHostFixture sharedOmniSharpHostFixture)
            : base(output, sharedOmniSharpHostFixture)
        {
        }

        protected override string EndpointName => OmniSharpEndpoints.V2.GotoDefinition;

        [Theory]
        [InlineData("bar.cs")]
        [InlineData("bar.csx")]
        public async Task ReturnsMultiplePartialTypeDefinition(string filename)
        {
            var testFile = new TestFile(filename, @"
partial class {|def:Class|}
{
    Cla$$ss c;
}
partial class {|def:Class|}
{
}");

            await TestGoToSourceAsync(testFile);
        }

        [Fact]
        public async Task ReturnsMultiplePartialTypeDefinition_MultipleFiles()
        {
            var testFile1 = new TestFile("bar.cs", @"
partial class {|def:Class|}
{
    Cla$$ss c;
}
");

            var testFile2 = new TestFile("baz.cs", @"
partial class {|def:Class|}
{
}");

            await TestGoToSourceAsync(testFile1, testFile2);
        }

        protected override GotoDefinitionRequest CreateRequest(string fileName, int line, int column, bool wantMetadata, int timeout = 60000)
            => new GotoDefinitionRequest
            {
                FileName = fileName,
                Line = line,
                Column = column,
                WantMetadata = wantMetadata,
                Timeout = timeout
            };

        protected override IEnumerable<(int Line, int Column, string FileName, SourceGeneratedFileInfo SourceGeneratorInfo)> GetInfo(GotoDefinitionResponse response)
        {
            if (response.Definitions is null)
                yield break;

            foreach (var definition in response.Definitions)
            {
                yield return (definition.Location.Range.Start.Line, definition.Location.Range.Start.Column, definition.Location.FileName, definition.SourceGeneratedFileInfo);
            }
        }

        protected override MetadataSource GetMetadataSource(GotoDefinitionResponse response)
        {
            Assert.Single(response.Definitions);
            return response.Definitions[0].MetadataSource;
        }
    }
}
