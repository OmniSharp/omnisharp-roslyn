using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Text;
using Microsoft.Extensions.Logging;
using OmniSharp.Models.V2;

namespace OmniSharp.Roslyn.CSharp.Services.Testing
{
    public class TestMethodsDiscover
    {
        private readonly ILogger _logger;

        public TestMethodsDiscover(ILoggerFactory loggerFactory)
        {
            _logger = loggerFactory.CreateLogger<TestMethodsDiscover>();
        }

        public async Task<IEnumerable<string>> FindTestActions(
            Workspace workspace,
            GetCodeActionsRequest request)
        {
            var solution = workspace.CurrentSolution;
            var document = solution.GetDocumentIdsWithFilePath(request.FileName)
                                   .Select(id => solution.GetDocument(id))
                                   .FirstOrDefault();
            var project = document.Project;
            var compilation = await project.GetCompilationAsync();
            var sourceText = await document.GetTextAsync();
            var span = GetTextSpan(request, sourceText);

            var syntaxTree = await document.GetSyntaxTreeAsync();
            var root = await syntaxTree.GetRootAsync() as CompilationUnitSyntax;
            var sematicModel = compilation.GetSemanticModel(syntaxTree);

            var testMethods = from declaration in root.DescendantNodes().OfType<MethodDeclarationSyntax>()
                              where IsSelectedTestMethod(declaration, span, sematicModel)
                              select GetFullQualifiedMethodNames(declaration, sematicModel);

            return testMethods.ToList();
        }

        private bool IsSelectedTestMethod(
            MethodDeclarationSyntax node,
            TextSpan selection,
            SemanticModel sematicModel)
        {
            if (!node.Span.Contains(selection))
            {
                return false;
            }

            return node.DescendantNodes()
                       .OfType<AttributeSyntax>()
                       .Select(attr => sematicModel.GetTypeInfo(attr).Type)
                       .Any(IsDerivedFromFactAttribute);
        }

        private static bool IsDerivedFromFactAttribute(ITypeSymbol symbol)
        {
            string fullName;
            do
            {
                fullName = $"{symbol.ContainingNamespace}.{symbol.Name}";
                if (fullName == "Xunit.FactAttribute")
                {
                    return true;
                }

                symbol = symbol.BaseType;
            } while (symbol.Name != "Object");

            return false;
        }

        private static string GetFullQualifiedMethodNames(
            MethodDeclarationSyntax node,
            SemanticModel sematicModel)
        {
            var s = sematicModel.GetDeclaredSymbol(node);
            return $"{s.ContainingType}.{s.Name}";
        }

        // reusable, copied from CodeActionHelper
        private static TextSpan GetTextSpan(
            ICodeActionRequest request,
            SourceText sourceText)
        {
            if (request.Selection != null)
            {
                var startPosition = sourceText.Lines.GetPosition(new LinePosition(request.Selection.Start.Line, request.Selection.Start.Column));
                var endPosition = sourceText.Lines.GetPosition(new LinePosition(request.Selection.End.Line, request.Selection.End.Column));
                return TextSpan.FromBounds(startPosition, endPosition);
            }

            var position = sourceText.Lines.GetPosition(new LinePosition(request.Line, request.Column));
            return new TextSpan(position, 1);
        }
    }
}