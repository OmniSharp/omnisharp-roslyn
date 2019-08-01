using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using System.Linq;

namespace OmniSharp.Extensions
{
    public static class SyntaxExtensions
    {
        public static string GetNamespace(this SyntaxNode self)
        {
            var @namespace = self.AncestorsAndSelf().OfType<NamespaceDeclarationSyntax>().FirstOrDefault();
            if (@namespace != null)
            {
                return @namespace.Name.ToString();
            }
            return string.Empty;
        }

        public static SyntaxList<UsingDirectiveSyntax> GetUsings(this SyntaxNode self)
        {
            if (self is NamespaceDeclarationSyntax @namespace)
            {
                return @namespace.Usings;
            }
            else if (self is CompilationUnitSyntax compilationUnit)
            {
                return compilationUnit.Usings;
            }
            return default;
        }
    }
}
