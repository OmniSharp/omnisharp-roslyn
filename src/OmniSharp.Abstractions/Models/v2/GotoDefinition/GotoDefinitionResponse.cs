#nullable enable

using OmniSharp.Models.Metadata;
using OmniSharp.Models.v1.SourceGeneratedFile;
using System.Collections.Generic;

namespace OmniSharp.Models.V2.GotoDefinition
{
    public record GotoDefinitionResponse
    {
        public List<Definition>? Definitions { get; init; }
    }

    public record Definition
    {
        public Location Location { get; init; } = null!;
        public MetadataSource? MetadataSource { get; init; }
        public SourceGeneratedFileInfo? SourceGeneratedFileInfo { get; init; }
    }
}
