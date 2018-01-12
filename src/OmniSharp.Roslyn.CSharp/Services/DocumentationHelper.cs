using Microsoft.CodeAnalysis;
using OmniSharp.Roslyn.CSharp.Services.Documentation;

namespace OmniSharp.Roslyn.CSharp.Services
{
    public static class DocumentationHelper
    {
        public static string GetParameterDocumentation(IParameterSymbol parameter, string lineEnding = "\n")
        {
            var contaningSymbolDef = parameter.ContainingSymbol.OriginalDefinition;
            return DocumentationConverter.GetStructuredDocumentation(contaningSymbolDef.GetDocumentationCommentXml(), lineEnding).GetParameterText(parameter.Name);
        }

        public static string GetTypeParameterDocumentation(ITypeParameterSymbol typeParam, string lineEnding = "\n")
        {
            var contaningSymbol = typeParam.ContainingSymbol;
            return DocumentationConverter.GetStructuredDocumentation(contaningSymbol.GetDocumentationCommentXml(), lineEnding).GetTypeParameterText(typeParam.Name);
        }

        public static string GetAliasDocumentation(IAliasSymbol alias, string lineEnding = "\n")
        {
            var target = alias.Target;
            return DocumentationConverter.GetStructuredDocumentation(target.GetDocumentationCommentXml(), lineEnding).SummaryText;
        }
    }
}
