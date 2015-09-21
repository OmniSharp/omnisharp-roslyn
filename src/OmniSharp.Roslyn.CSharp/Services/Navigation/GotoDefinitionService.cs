using System;
using System.Collections.Generic;
using System.Composition;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.FindSymbols;
using Microsoft.CodeAnalysis.Text;
using OmniSharp;
using OmniSharp.Mef;
using OmniSharp.Models;

namespace OmniSharp.Roslyn.CSharp.Services.Navigation
{
    [OmniSharpHandler(typeof(RequestHandler<GotoDefinitionRequest, GotoDefinitionResponse>), LanguageNames.CSharp)]
    public class GotoDefinitionService : RequestHandler<GotoDefinitionRequest, GotoDefinitionResponse>
    {
        private OmnisharpWorkspace _workspace;

        [ImportingConstructor]
        public GotoDefinitionService(OmnisharpWorkspace workspace)
        {
            _workspace = workspace;
        }

        public async Task<GotoDefinitionResponse> Handle(GotoDefinitionRequest request)
        {
            var quickFixes = new List<QuickFix>();

            var document = _workspace.GetDocument(request.FileName);
            var response = new GotoDefinitionResponse();
            if (document != null)
            {
                var semanticModel = await document.GetSemanticModelAsync();
                var syntaxTree = semanticModel.SyntaxTree;
                var sourceText = await document.GetTextAsync();
                var position = sourceText.Lines.GetPosition(new LinePosition(request.Line - 1, request.Column - 1));
                var symbol = SymbolFinder.FindSymbolAtPosition(semanticModel, position, _workspace);

                if (symbol != null)
                {
                    var location = symbol.Locations.First();

                    if (location.IsInSource)
                    {
                        var lineSpan = symbol.Locations.First().GetMappedLineSpan();
                        response = new GotoDefinitionResponse
                        {
                            FileName = lineSpan.Path,
                            Line = lineSpan.StartLinePosition.Line + 1,
                            Column = lineSpan.StartLinePosition.Character + 1
                        };
                    }
                    else if (location.IsInMetadata && request.WantMetadata)
                    {
                        var cancellationSource = new CancellationTokenSource(TimeSpan.FromMilliseconds(request.Timeout));
                        var metadataDocument = await MetadataHelper.GetDocumentFromMetadata(document.Project, symbol, cancellationSource.Token);
                        if (metadataDocument != null)
                        {
                            cancellationSource = new CancellationTokenSource(TimeSpan.FromMilliseconds(request.Timeout));
                            var metadataLocation = await MetadataHelper.GetSymbolLocationFromMetadata(symbol, metadataDocument, cancellationSource.Token);
                            var lineSpan = metadataLocation.GetMappedLineSpan();

                            response = new GotoDefinitionResponse
                            {
                                Line = lineSpan.StartLinePosition.Line + 1,
                                Column = lineSpan.StartLinePosition.Character + 1,
                                MetadataSource = new MetadataSource()
                                {
                                    AssemblyName = symbol.ContainingAssembly.Name,
                                    ProjectName = document.Project.Name,
                                    TypeName = MetadataHelper.GetSymbolName(symbol)
                                },
                            };
                        }
                    }
                }
            }

            return response;
        }
    }
}
