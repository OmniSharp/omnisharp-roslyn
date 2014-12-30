using System.Collections.Generic;
using System.Threading.Tasks;
using Microsoft.AspNet.Mvc;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Text;
using OmniSharp.Models;

namespace OmniSharp
{
    public partial class OmnisharpController
    {
        [HttpPost("gettestcontext")]
        public async Task<IActionResult> GetTestContext([FromBody]TestCommandRequest request)
        {
            var response = await GetResponseAsync(request);
            var testCommand = TestCommand(request, response.TypeName, response.MethodName);

            var testContextResponse = new GetTestContextResponse
            {
                TestCommand = testCommand
            };
            return new ObjectResult(testContextResponse);
        }

        [HttpPost("getcontext")]
        public async Task<IActionResult> GetContext([FromBody]TestCommandRequest request)
        {
            var response = await GetResponseAsync(request);
            if (response.TypeName != null)
            {
                return new ObjectResult(response);
            }
            else
            {
                return new HttpNotFoundResult();
            }
        }

        private async Task<GetContextResponse> GetResponseAsync(TestCommandRequest request)
        {
            _workspace.EnsureBufferUpdated(request);

            var document = _workspace.GetDocument(request.FileName);
            var response = new GetContextResponse();
            if (document != null)
            {
                var semanticModel = await document.GetSemanticModelAsync();
                var syntaxTree = semanticModel.SyntaxTree;
                var sourceText = await document.GetTextAsync();
                var position = sourceText.Lines.GetPosition(new LinePosition(request.Line - 1, request.Column - 1));
                var node = syntaxTree.GetRoot().FindToken(position).Parent;
                var method = node.FirstAncestorOrSelf<MethodDeclarationSyntax>();
                var type = node.FirstAncestorOrSelf<TypeDeclarationSyntax>();
                var namespaceDeclaration = node.FirstAncestorOrSelf<NamespaceDeclarationSyntax>();

                response.MethodName = method.Identifier.ToString();
                response.TypeName = type.Identifier.ToString();

                if (namespaceDeclaration != null)
                {
                    response.TypeName = namespaceDeclaration.Name.ToString() + "." + response.TypeName;
                }
            }
            return response;

        }

        private string TestCommand(TestCommandRequest request, string typeName, string methodName)
        {
            //TODO: get test commands from config
            string testCommand = "k test";
            switch (request.Type)
            {
                case TestCommandRequest.TestCommandType.Fixture:
                    testCommand = "k test --test {{TypeName}}";
                    break;
                case TestCommandRequest.TestCommandType.Single:
                    testCommand = "k test --test {{TypeName}}.{{MethodName}}";
                    break;
            }

            testCommand = testCommand.Replace("{{TypeName}}", typeName)
                            .Replace("{{MethodName}}", methodName);

            return testCommand;
        }
    }
}