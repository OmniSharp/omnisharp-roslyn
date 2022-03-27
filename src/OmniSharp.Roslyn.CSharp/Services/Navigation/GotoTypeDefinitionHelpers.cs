#nullable enable

using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.FindSymbols;
using Microsoft.CodeAnalysis.Operations;
using OmniSharp.Extensions;
using OmniSharp.Models.v1.SourceGeneratedFile;
using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;

namespace OmniSharp.Roslyn.CSharp.Services.Navigation
{
    internal static class GotoTypeDefinitionHelpers
    {
        internal static async Task<ITypeSymbol?> GetTypeOfSymbol(Document document, int line, int column, CancellationToken cancellationToken)
        {
            var sourceText = await document.GetTextAsync(cancellationToken);
            var position = sourceText.GetPositionFromLineAndOffset(line, column);
            var symbol = await SymbolFinder.FindSymbolAtPositionAsync(document, position, cancellationToken);

            return symbol switch
            {
                ILocalSymbol localSymbol => localSymbol.Type,
                IFieldSymbol fieldSymbol => fieldSymbol.Type,
                IPropertySymbol propertySymbol => propertySymbol.Type,
                IParameterSymbol parameterSymbol => parameterSymbol.Type,
                _ => null
            };
        }

        internal static async Task<FileLinePositionSpan?> GetMetadataMappedSpan(
            Document document,
            ISymbol symbol,
            IExternalSourceService externalSourceService,
            CancellationToken cancellationToken)
        {
            var (metadataDocument, _) = await externalSourceService.GetAndAddExternalSymbolDocument(document.Project, symbol, cancellationToken);
            if (metadataDocument != null)
            {
                var metadataLocation = await externalSourceService.GetExternalSymbolLocation(symbol, metadataDocument, cancellationToken);
                return metadataLocation.GetMappedLineSpan();
            }

            return null;
        }

        internal static SourceGeneratedFileInfo? GetSourceGeneratedFileInfo(OmniSharpWorkspace workspace, Location location)
        {
            Debug.Assert(location.IsInSource);
            var document = workspace.CurrentSolution.GetDocument(location.SourceTree);
            if (document is not SourceGeneratedDocument)
            {
                return null;
            }

            return new SourceGeneratedFileInfo
            {
                ProjectGuid = document.Project.Id.Id,
                DocumentGuid = document.Id.Id
            };
        }
    }
}
