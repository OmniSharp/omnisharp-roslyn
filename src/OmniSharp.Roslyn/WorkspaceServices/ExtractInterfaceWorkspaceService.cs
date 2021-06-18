using System.Collections.Generic;
using System.Collections.Immutable;
using System.Composition;
using System.Threading.Tasks;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.ExternalAccess.OmniSharp.ExtractInterface;

namespace OmniSharp
{
    [Shared]
    [Export(typeof(IOmniSharpExtractInterfaceOptionsService))]
    internal class ExtractInterfaceWorkspaceService : IOmniSharpExtractInterfaceOptionsService
    {
        [ImportingConstructor]
        public ExtractInterfaceWorkspaceService()
        {
        }

        public Task<OmniSharpExtractInterfaceOptionsResult> GetExtractInterfaceOptionsAsync(List<ISymbol> extractableMembers, string defaultInterfaceName)
        {
            return Task.FromResult(new OmniSharpExtractInterfaceOptionsResult(
                isCancelled: false,
                extractableMembers.ToImmutableArray(),
                defaultInterfaceName,
                $"{defaultInterfaceName}.cs",
                OmniSharpExtractInterfaceOptionsResult.OmniSharpExtractLocation.SameFile));
        }
    }
}
