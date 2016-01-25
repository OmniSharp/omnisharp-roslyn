using System.Collections.Generic;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using OmniSharp.Models;
using OmniSharp.Roslyn.CSharp.Services.CodeActions;
using OmniSharp.Roslyn.CSharp.Services.Refactoring;
using OmniSharp.Tests;
using Xunit;

namespace OmniSharp.Roslyn.CSharp.Tests
{
    public class CodingActionsFacts
    {
        private readonly ILoggerFactory _loggerFactory;

        public CodingActionsFacts()
        {
            _loggerFactory = new LoggerFactory();
            _loggerFactory.AddConsole();
        }

        [Fact(Skip = "Microsoft.CodeAnalysis.Shared.Extensions.CommonSyntaxTokenExtensions is renamed. ICSharpCode.NRefactory6.CSharp.SyntaxExtensions is not update-to-date")]
        public async Task Can_get_code_actions()
        {
            var source =
                @"using System;
                  public class Class1
                  {
                      public void Whatever()
                      {
                          int$ i = 1;
                      }
                  }";

            var refactorings = await FindRefactoringsAsync(source);
            Assert.Contains("Use 'var' keyword", refactorings);
        }

        private async Task<IEnumerable<string>> FindRefactoringsAsync(string source)
        {
            var loader = new TestOmnisharpAssemblyLoader(_loggerFactory.CreateLogger<CodingActionsFacts>());
            var workspace = await TestHelpers.CreateSimpleWorkspace(source);
            var controller = new GetCodeActionsService(workspace, new[] { new NRefactoryCodeActionProvider(loader) });
            var request = CreateRequest(source);
            var response = await controller.Handle(request);
            return response.CodeActions;
        }

        private GetCodeActionRequest CreateRequest(string source, string fileName = "dummy.cs")
        {
            var lineColumn = TestHelpers.GetLineAndColumnFromDollar(source);
            return new GetCodeActionRequest
            {
                Line = lineColumn.Line,
                Column = lineColumn.Column,
                FileName = fileName,
                Buffer = source.Replace("$", ""),
            };
        }
    }
}
