using System.Collections.Immutable;
using System.Composition;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.ExternalAccess.OmniSharp.ExtractClass;

namespace OmniSharp
{
    [Shared]
    [Export(typeof(IOmniSharpExtractClassOptionsService))]
    internal class ExtractClassWorkspaceService : IOmniSharpExtractClassOptionsService
    {
        [ImportingConstructor]
        public ExtractClassWorkspaceService()
        {
        }

        public Task<OmniSharpExtractClassOptions> GetExtractClassOptionsAsync(Document document, INamedTypeSymbol originalType, ISymbol selectedMember)
        {
            var symbolsToUse = selectedMember == null ? originalType.GetMembers().Where(member => member switch
            {
                IMethodSymbol methodSymbol => methodSymbol.MethodKind == MethodKind.Ordinary,
                IFieldSymbol fieldSymbol => !fieldSymbol.IsImplicitlyDeclared,
                _ => member.Kind == SymbolKind.Property || member.Kind == SymbolKind.Event
            }) : new ISymbol[] { selectedMember };

            var memberAnalysisResults = symbolsToUse.Select(m => new OmniSharpExtractClassMemberAnalysisResult(m, makeAbstract: false)).ToImmutableArray();
            const string name = "NewBaseType";
            return Task.FromResult(new OmniSharpExtractClassOptions($"{name}.cs", name, true, memberAnalysisResults));
        }
    }
}
