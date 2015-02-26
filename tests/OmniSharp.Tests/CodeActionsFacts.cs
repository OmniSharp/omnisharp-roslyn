#if ASPNET50
using System.Collections.Generic;
using System.Threading.Tasks;
using OmniSharp.Models;
using OmniSharp.Services;
using Xunit;

namespace OmniSharp.Tests
{
    public class CodingActionsFacts
    {
        [Fact]
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
            var workspace = TestHelpers.CreateSimpleWorkspace(source);
            var controller = new CodeActionController(workspace, new[] { new NRefactoryCodeActionProvider() });
            var request = CreateRequest(source);
            var response = await controller.GetCodeActions(request);
            return response.CodeActions;
        }

        private CodeActionRequest CreateRequest(string source, string fileName = "dummy.cs")
        {
            var lineColumn = TestHelpers.GetLineAndColumnFromDollar(source);
            return new CodeActionRequest
            {
                Line = lineColumn.Line,
                Column = lineColumn.Column,
                FileName = fileName,
                Buffer = source.Replace("$", ""),
            };
        }
    }
}
#endif