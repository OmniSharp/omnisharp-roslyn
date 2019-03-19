using System.Composition;
using System.Threading.Tasks;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.FindSymbols;
using Microsoft.CodeAnalysis.Text;
using OmniSharp.Mef;
using OmniSharp.Models.TypeLookup;
using OmniSharp.Options;
using OmniSharp.Roslyn.CSharp.Services.Documentation;

namespace OmniSharp.Roslyn.CSharp.Services.Types
{
    [OmniSharpHandler(OmniSharpEndpoints.TypeLookup, LanguageNames.CSharp)]
    public class TypeLookupService : IRequestHandler<TypeLookupRequest, TypeLookupResponse>
    {
        private readonly FormattingOptions _formattingOptions;
        private readonly OmniSharpWorkspace _workspace;
        private static readonly SymbolDisplayFormat DefaultFormat = SymbolDisplayFormat.FullyQualifiedFormat.
            WithGlobalNamespaceStyle(SymbolDisplayGlobalNamespaceStyle.Omitted).
            WithMiscellaneousOptions(SymbolDisplayMiscellaneousOptions.None).
            WithMiscellaneousOptions(SymbolDisplayMiscellaneousOptions.EscapeKeywordIdentifiers);

        // default from symbol.ToMinimalDisplayString + IncludeConstantValue
        private static readonly SymbolDisplayFormat MinimalFormat = SymbolDisplayFormat.MinimallyQualifiedFormat.WithMemberOptions(
            SymbolDisplayMemberOptions.IncludeParameters |
            SymbolDisplayMemberOptions.IncludeType |
            SymbolDisplayMemberOptions.IncludeRef |
            SymbolDisplayMemberOptions.IncludeContainingType |
            SymbolDisplayMemberOptions.IncludeConstantValue
         );

        [ImportingConstructor]
        public TypeLookupService(OmniSharpWorkspace workspace, FormattingOptions formattingOptions)
        {
            _workspace = workspace;
            _formattingOptions = formattingOptions;
        }

        public async Task<TypeLookupResponse> Handle(TypeLookupRequest request)
        {
            var document = _workspace.GetDocument(request.FileName);
            var response = new TypeLookupResponse();
            if (document != null)
            {
                var semanticModel = await document.GetSemanticModelAsync();
                var sourceText = await document.GetTextAsync();
                var position = sourceText.Lines.GetPosition(new LinePosition(request.Line, request.Column));
                var symbol = await SymbolFinder.FindSymbolAtPositionAsync(semanticModel, position, _workspace);
                if (symbol != null)
                {
                    response.Type = symbol.Kind == SymbolKind.NamedType ? 
                        symbol.ToDisplayString(DefaultFormat) : 
                        symbol.ToMinimalDisplayString(semanticModel, position, MinimalFormat);

                    if (request.IncludeDocumentation)
                    {
                        response.Documentation = DocumentationConverter.ConvertDocumentation(symbol.GetDocumentationCommentXml(), _formattingOptions.NewLine);
                        response.StructuredDocumentation = DocumentationConverter.GetStructuredDocumentation(symbol, _formattingOptions.NewLine);
                    }
                }
            }

            return response;
        }
    }
}
