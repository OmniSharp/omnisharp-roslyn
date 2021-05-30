#nullable enable

using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.FindSymbols;
using OmniSharp.Extensions;
using OmniSharp.Options;
using System.Threading.Tasks;

namespace OmniSharp.Roslyn.CSharp.Services.Navigation
{
    internal static class GoToDefinitionHelpers
    {
        internal static async Task<ISymbol?> GetDefinitionSymbol(Document document, int line, int column)
        {
            var sourceText = await document.GetTextAsync();
            var position = sourceText.GetPositionFromLineAndOffset(line, column);
            var symbol = await SymbolFinder.FindSymbolAtPositionAsync(document, position);

            return symbol switch
            {
                INamespaceSymbol => null,
                // Always prefer the partial implementation over the definition
                IMethodSymbol { IsPartialDefinition: true, PartialImplementationPart: var impl } => impl,
                // Don't return property getters/settings/initers
                IMethodSymbol { AssociatedSymbol: IPropertySymbol } => null,
                _ => symbol
            };
        }

        internal static async Task<FileLinePositionSpan?> GetMetadataMappedSpan(
            Document document,
            ISymbol symbol,
            ExternalSourceServiceFactory externalSourceServiceFactory,
            IExternalSourceService externalSourceService,
            OmniSharpOptions options,
            int timeout)
        {

            var cancellationToken = externalSourceServiceFactory.CreateCancellationToken(options, timeout);
            var (metadataDocument, _) = await externalSourceService.GetAndAddExternalSymbolDocument(document.Project, symbol, cancellationToken);
            if (metadataDocument != null)
            {
                cancellationToken = externalSourceServiceFactory.CreateCancellationToken(options, timeout);
                var metadataLocation = await externalSourceService.GetExternalSymbolLocation(symbol, metadataDocument, cancellationToken);
                return metadataLocation.GetMappedLineSpan();
            }

            return null;
        }
    }
}
