using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNet.Mvc;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Text;
using OmniSharp.Models;

namespace OmniSharp
{
    public class TestCommandController
    {
        private OmnisharpWorkspace _workspace;
        private IEnumerable<ITestCommandProvider> _testCommandProviders;

        public TestCommandController(OmnisharpWorkspace workspace,
                                     IEnumerable<ITestCommandProvider> testCommandProviders)
        {
            _workspace = workspace;
            _testCommandProviders = testCommandProviders;
        }

        [HttpPost("gettestcontext")]
        public async Task<GetTestCommandResponse> GetTestCommand(TestCommandRequest request)
        {
            var quickFixes = new List<QuickFix>();

            var document = _workspace.GetDocument(request.FileName);
            var response = new GetTestCommandResponse();
            if (document != null)
            {
                var semanticModel = await document.GetSemanticModelAsync();
                var syntaxTree = semanticModel.SyntaxTree;
                var sourceText = await document.GetTextAsync();
                var position = sourceText.Lines.GetPosition(new LinePosition(request.Line - 1, request.Column - 1));
                var node = syntaxTree.GetRoot().FindToken(position).Parent;

                SyntaxNode method = node.FirstAncestorOrSelf<MethodDeclarationSyntax>();
                SyntaxNode type = GetTypeDeclaration(node);

                if (type == null)
                {
                    return response;
                }

                var symbol = semanticModel.GetDeclaredSymbol(method ?? type);
                var context = new TestContext(document.Project.FilePath, request.Type, symbol);

                response.TestCommand = _testCommandProviders
                    .Select(t => t.GetTestCommand(context))
                    .FirstOrDefault(c => c != null);

                var directory = Path.GetDirectoryName(document.Project.FilePath);
                response.Directory = directory;
            }

            return response;
        }

        private static SyntaxNode GetTypeDeclaration(SyntaxNode node)
        {
            var type = node.FirstAncestorOrSelf<ClassDeclarationSyntax>();

            if (type == null)
            {
                type = node.SyntaxTree.GetRoot()
                        .DescendantNodes().OfType<ClassDeclarationSyntax>()
                        .FirstOrDefault();
            }

            return type;
        }
    }
}