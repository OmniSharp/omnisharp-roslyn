using System;
using System.Collections.Generic;
using System.Composition;
using System.Linq;
using System.Threading.Tasks;
using OmniSharp.Extensions;
using OmniSharp.Mef;
using OmniSharp.Models.V2.GotoTypeDefinition;
using OmniSharp.Models.Metadata;
using OmniSharp.Models.v1.SourceGeneratedFile;
using OmniSharp.Models.V2;
using OmniSharp.Roslyn;
using OmniSharp.Utilities;
using Location = OmniSharp.Models.V2.Location;
using Range = OmniSharp.Models.V2.Range;

namespace OmniSharp.Cake.Services.RequestHandlers.Navigation
{
    [OmniSharpHandler(OmniSharpEndpoints.V2.GotoTypeDefinition, Constants.LanguageNames.Cake), Shared]
    public class GotoTypeDefinitionV2Handler : CakeRequestHandler<GotoTypeDefinitionRequest, GotoTypeDefinitionResponse>
    {
        private readonly MetadataExternalSourceService _metadataExternalSourceService;

        [ImportingConstructor]
        public GotoTypeDefinitionV2Handler(
            OmniSharpWorkspace workspace,
            MetadataExternalSourceService metadataExternalSourceService)
            : base(workspace)
        {
            _metadataExternalSourceService = metadataExternalSourceService ?? throw new ArgumentNullException(nameof(metadataExternalSourceService));
        }

        protected override async Task<GotoTypeDefinitionResponse> TranslateResponse(GotoTypeDefinitionResponse response, GotoTypeDefinitionRequest request)
        {
            var definitions = new List<TypeDefinition>();
            foreach (var definition in response.Definitions ?? Enumerable.Empty<TypeDefinition>())
            {
                var file = definition.Location.FileName;

                if (string.IsNullOrEmpty(file) || !file.Equals(Constants.Paths.Generated))
                {
                    if (PlatformHelper.IsWindows && !string.IsNullOrEmpty(file))
                    {
                        file = file.Replace('/', '\\');
                    }

                    definitions.Add(new TypeDefinition
                    {
                        MetadataSource = definition.MetadataSource,
                        SourceGeneratedFileInfo = definition.SourceGeneratedFileInfo,
                        Location = new Location
                        {
                            FileName = file,
                            Range = definition.Location.Range
                        }
                    });

                    continue;
                }

                if (!request.WantMetadata)
                {
                    continue;
                }

                var aliasLocations = await GotoTypeDefinitionHandlerHelper.GetAliasFromMetadataAsync(
                    Workspace,
                    request.FileName,
                    definition.Location.Range.End.Line,
                    request.Timeout,
                    _metadataExternalSourceService
                );

                definitions.AddRange(
                    aliasLocations.Select(loc =>
                        new TypeDefinition
                        {
                            Location = new Location
                            {
                                FileName = loc.MetadataDocument.FilePath ?? loc.MetadataDocument.Name,
                                Range = new Range
                                {
                                    Start = new Point
                                    {
                                        Column = loc.LineSpan.StartLinePosition.Character,
                                        Line = loc.LineSpan.StartLinePosition.Line
                                    },
                                    End = new Point
                                    {
                                        Column = loc.LineSpan.EndLinePosition.Character,
                                        Line = loc.LineSpan.EndLinePosition.Line
                                    },
                                }
                            },
                            MetadataSource = new MetadataSource
                            {
                                AssemblyName = loc.Symbol.ContainingAssembly.Name,
                                ProjectName = loc.Document.Project.Name,
                                TypeName = loc.Symbol.GetSymbolName()
                            },
                            SourceGeneratedFileInfo = new SourceGeneratedFileInfo
                            {
                                DocumentGuid = loc.Document.Id.Id,
                                ProjectGuid = loc.Document.Id.ProjectId.Id
                            }
                        })
                        .ToList());
            }

            return new GotoTypeDefinitionResponse
            {
                Definitions = definitions
            };
        }
    }
}
