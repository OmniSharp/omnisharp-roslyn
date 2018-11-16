using System;
using System.Collections.Generic;
using System.Composition;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Text;
using OmniSharp.ConfigurationManager;
using OmniSharp.Mef;
using OmniSharp.Models;
using OmniSharp.Models.TestCommand;
using OmniSharp.Services;

namespace OmniSharp.Roslyn.CSharp.Services.TestCommands
{
    [OmniSharpHandler(OmniSharpEndpoints.TestCommand, LanguageNames.CSharp)]
    public class TestCommandService : IRequestHandler<TestCommandRequest, GetTestCommandResponse>
    {
        private OmniSharpWorkspace _workspace;
        public IEnumerable<ITestCommandProvider> _testCommandProviders;
        public OmniSharpConfiguration _config;

        [ImportingConstructor]
        public TestCommandService(OmniSharpWorkspace workspace, [ImportMany] IEnumerable<ITestCommandProvider> testCommandProviders)
        {
            _workspace = workspace;
            _testCommandProviders = testCommandProviders;
        }

        public async Task<GetTestCommandResponse> Handle(TestCommandRequest request)
        {
            var quickFixes = new List<QuickFix>();

            var document2 = _workspace.CurrentSolution.Projects.SelectMany(p => p.Documents)
                .GroupBy(x => x.FilePath).Select(f => f.FirstOrDefault());
            var document = _workspace.GetDocument(document2.Where(doc => Path.GetFileName(doc.Name).ToLower() == Path.GetFileName(request.FileName).ToLower()).FirstOrDefault().Name);
            var response = new GetTestCommandResponse();

            var testCommands = ConfigurationLoader.Config.TestCommands != null ? ConfigurationLoader.Config.TestCommands : _config.TestCommands;
            string testCommand = testCommands.All;

            if (document != null)
            {
                var semanticModel = await document.GetSemanticModelAsync();
                var syntaxTree = semanticModel.SyntaxTree;
                var sourceText = await document.GetTextAsync();
                var position = sourceText.Lines.GetPosition(new LinePosition(request.Line, request.Column));
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

                switch (request.Type)
                {
                    case TestCommandType.All:
                        testCommand = testCommands.All;
                        break;
                    case TestCommandType.Fixture:
                        testCommand = testCommands.Fixture;
                        break;
                    case TestCommandType.Single:
                        testCommand = testCommands.Single;
                        break;
                }

                //testCommand = testCommand.Replace("{{AssemblyPath}}", document.Project.OutputFilePath)
                //    .Replace("{{TypeName}}", response.TestCommand);
                testCommand = testCommand.Replace("{{AssemblyPath}}", document.Project.FilePath)
                    .Replace("{{TypeName}}", response.TestCommand);
                //.Replace("{{MethodName}}", context.Symbol.OriginalDefinition.Locations.ToString());

                response.TestCommand = testCommand;

                var directory = Path.GetDirectoryName(document.Project.OutputFilePath);
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
