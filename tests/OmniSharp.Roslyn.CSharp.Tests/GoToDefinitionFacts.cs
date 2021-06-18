using OmniSharp.Models.GotoDefinition;
using OmniSharp.Roslyn.CSharp.Services.Navigation;
using OmniSharp.Models.Metadata;
using TestUtility;
using Xunit.Abstractions;
using System.Collections.Generic;
using OmniSharp.Models.v1.SourceGeneratedFile;

namespace OmniSharp.Roslyn.CSharp.Tests
{
    public class GoToDefinitionFacts : AbstractGoToDefinitionFacts<GotoDefinitionService, GotoDefinitionRequest, GotoDefinitionResponse>
    {
        public GoToDefinitionFacts(ITestOutputHelper output, SharedOmniSharpHostFixture sharedOmniSharpHostFixture)
            : base(output, sharedOmniSharpHostFixture)
        {
        }

        protected override string EndpointName => OmniSharpEndpoints.GotoDefinition;

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
            if (response.IsEmpty)
            {
                yield break;
            }

            yield return (response.Line, response.Column, response.FileName, response.SourceGeneratedInfo);
        }

        protected override MetadataSource GetMetadataSource(GotoDefinitionResponse response)
            => response.MetadataSource;
    }
}
