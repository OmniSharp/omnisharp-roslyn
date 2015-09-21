using System.Composition;
﻿using System.Threading.Tasks;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.FindSymbols;
using Microsoft.CodeAnalysis.Text;
using OmniSharp.Mef;
using OmniSharp.Models;
using OmniSharp.Options;
using OmniSharp.Roslyn.CSharp.Services.Documentation;

namespace OmniSharp.Roslyn.CSharp.Services.Types
{
    [OmniSharpHandler(typeof(RequestHandler<TypeLookupRequest, TypeLookupResponse>), LanguageNames.CSharp)]
    public class TypeLookupService : RequestHandler<TypeLookupRequest, TypeLookupResponse>
    {
        private readonly FormattingOptions _formattingOptions;
        private readonly OmnisharpWorkspace _workspace;

        [ImportingConstructor]
        public TypeLookupService(OmnisharpWorkspace workspace, FormattingOptions formattingOptions)
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
                var position = sourceText.Lines.GetPosition(new LinePosition(request.Line - 1, request.Column - 1));
                var symbol = SymbolFinder.FindSymbolAtPosition(semanticModel, position, _workspace);
                if (symbol != null)
                {
                    //non regular C# code semantics (interactive, script) don't allow namespaces
                    if (document.SourceCodeKind == SourceCodeKind.Regular && symbol.Kind == SymbolKind.NamedType)
                    {
                        response.Type = $"{symbol.ContainingNamespace.ToDisplayString()}.{symbol.ToDisplayString(SymbolDisplayFormat.MinimallyQualifiedFormat)}";
                    }
                    else
                    {
                        response.Type = symbol.ToDisplayString(SymbolDisplayFormat.MinimallyQualifiedFormat);
                    }

                    if (request.IncludeDocumentation)
                    {
                        response.Documentation = DocumentationConverter.ConvertDocumentation(symbol.GetDocumentationCommentXml(), _formattingOptions.NewLine);
                    }
                }
            }
            return response;
        }
    }
}
