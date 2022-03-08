using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.FindSymbols;
using Microsoft.CodeAnalysis.Text;
using OmniSharp.Roslyn;
using OmniSharp.Roslyn.CSharp.Services;

namespace OmniSharp.Cake.Services.RequestHandlers.Navigation
{
    public static class GotoTypeDefinitionHandlerHelper
    {
        private const int MethodLineOffset = 3;
        private const int PropertyLineOffset = 7;

        internal static async Task<IEnumerable<Alias>> GetAliasFromExternalSourceAsync(
            OmniSharpWorkspace workspace,
            string fileName,
            int line,
            int timeout,
            IExternalSourceService externalSourceService)
        {
            var document = workspace.GetDocument(fileName);
            var lineIndex = line + MethodLineOffset;
            int column;

            if (document == null)
            {
                return Enumerable.Empty<Alias>();
            }

            var semanticModel = await document.GetSemanticModelAsync();
            var sourceText = await document.GetTextAsync();
            var sourceLine = sourceText.Lines[lineIndex].ToString();
            if (sourceLine.Contains("(Context"))
            {
                column = sourceLine.IndexOf("(Context", StringComparison.Ordinal);
            }
            else
            {
                lineIndex = line + PropertyLineOffset;
                sourceLine = sourceText.Lines[lineIndex].ToString();
                if (sourceLine.Contains("(Context"))
                {
                    column = sourceLine.IndexOf("(Context", StringComparison.Ordinal);
                }
                else
                {
                    return Enumerable.Empty<Alias>();
                }
            }

            if (column > 0 && sourceLine[column - 1] == '>')
            {
                column = sourceLine.LastIndexOf("<", column, StringComparison.Ordinal);
            }

            var position = sourceText.Lines.GetPosition(new LinePosition(lineIndex, column));
            var symbol = await SymbolFinder.FindSymbolAtPositionAsync(semanticModel, position, workspace);

            if (symbol == null || symbol is INamespaceSymbol)
            {
                return Enumerable.Empty<Alias>();
            }
            if (symbol is IMethodSymbol method)
            {
                symbol = method.PartialImplementationPart ?? symbol;
            }

            var typeSymbol = symbol switch
            {
                ILocalSymbol localSymbol => localSymbol.Type,
                IFieldSymbol fieldSymbol => fieldSymbol.Type,
                IPropertySymbol propertySymbol => propertySymbol.Type,
                IParameterSymbol parameterSymbol => parameterSymbol.Type,
                _ => null
            };

            if (typeSymbol == null)
                return Enumerable.Empty<Alias>();

            var result = new List<Alias>();
            foreach (var location in typeSymbol.Locations)
            {
                if (!location.IsInMetadata)
                {
                    continue;
                }

                var cancellationSource = new CancellationTokenSource(TimeSpan.FromMilliseconds(timeout));
                var (metadataDocument, _) = await externalSourceService.GetAndAddExternalSymbolDocument(document.Project, typeSymbol, cancellationSource.Token);
                if (metadataDocument == null)
                {
                    continue;
                }

                cancellationSource = new CancellationTokenSource(TimeSpan.FromMilliseconds(timeout));
                var metadataLocation = await externalSourceService.GetExternalSymbolLocation(typeSymbol, metadataDocument, cancellationSource.Token);
                var lineSpan = metadataLocation.GetMappedLineSpan();

                result.Add(new Alias
                {
                    Document = document,
                    MetadataDocument = metadataDocument,
                    Symbol = typeSymbol,
                    Location = location,
                    LineSpan = lineSpan
                });
            }

            return result;
        }

        internal class Alias
        {
            public Document Document { get; set; }
            public ISymbol Symbol { get; set; }
            public Location Location { get; set; }
            public FileLinePositionSpan LineSpan { get; set; }
            public Document MetadataDocument { get; set; }
        }
    }
}
