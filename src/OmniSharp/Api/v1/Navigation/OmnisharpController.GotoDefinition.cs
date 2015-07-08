using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNet.Mvc;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.FindSymbols;
using Microsoft.CodeAnalysis.Text;
using OmniSharp.Models;

namespace OmniSharp
{
    public partial class OmnisharpController
    {
        private static Lazy<Type> _metadataAsSourceType = new Lazy<Type>(() =>
        {
            var name = new AssemblyName("Microsoft.CodeAnalysis.CSharp.Features, Version=1.0.0.0, Culture=neutral, PublicKeyToken=31bf3856ad364e35");
            var assembly = Assembly.Load(name);
            return assembly.GetType("Microsoft.CodeAnalysis.CSharp.MetadataAsSource.CSharpMetadataAsSourceService");
        });

        [HttpPost("gotodefinition")]
        public async Task<IActionResult> GotoDefinition(Request request)
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
                    else if (location.IsInMetadata)
                    {
                        response.MetadataSource = await GetMetadataSource(document, symbol);
                    }
                }
            }

            return new ObjectResult(response);
        }

        private static async Task<string> GetMetadataSource(Document document, ISymbol symbol) {
            var temporaryDocument = document.Project.AddDocument("dummyDocument", string.Empty);
            var topLevelSymbol = GetTopLevelContainingNamedType(symbol);

            var metadataAsSourceType = _metadataAsSourceType.Value;

            object service = Activator.CreateInstance(metadataAsSourceType,
                new object[] { temporaryDocument.Project.LanguageServices });
            var method = metadataAsSourceType.GetMethod("AddSourceToAsync");

            var result = await (Task<Document>)method.Invoke(service,
                new object[] { temporaryDocument, topLevelSymbol, new CancellationToken() });
            var source = await result.GetTextAsync();
            return source.ToString();
        }

        private static INamedTypeSymbol GetTopLevelContainingNamedType(ISymbol symbol)
        {
            // Traverse up until we find a named type that is parented by the namespace
            var topLevelNamedType = symbol;
            while (topLevelNamedType.ContainingSymbol != symbol.ContainingNamespace ||
                topLevelNamedType.Kind != SymbolKind.NamedType)
            {
                topLevelNamedType = topLevelNamedType.ContainingSymbol;
            }

            return (INamedTypeSymbol)topLevelNamedType;
        }

    }
}
