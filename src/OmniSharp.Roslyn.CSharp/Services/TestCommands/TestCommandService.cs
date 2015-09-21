using System.Collections.Generic;
using System.Composition;
using System.Composition.Hosting;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Text;
using OmniSharp.Mef;
using OmniSharp.Models;

namespace OmniSharp.Roslyn.CSharp.Services.TestCommands
{
    [OmniSharpHandler(typeof(RequestHandler<TestCommandRequest, GetTestCommandResponse>), LanguageNames.CSharp)]
    public class TestCommandService : RequestHandler<TestCommandRequest, GetTestCommandResponse>
    {
        private OmnisharpWorkspace _workspace;
        private IEnumerable<ITestCommandProvider> _testCommandProviders;

        [ImportingConstructor]
        public TestCommandService(OmnisharpWorkspace workspace, [ImportMany] IEnumerable<ITestCommandProvider> testCommandProviders)
        {
            _workspace = workspace;
            _testCommandProviders = testCommandProviders;
        }

        public async Task<GetTestCommandResponse> Handle(TestCommandRequest request)
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
