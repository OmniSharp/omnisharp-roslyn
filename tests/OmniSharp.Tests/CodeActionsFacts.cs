#if DNX451
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using OmniSharp.Models;
using OmniSharp.Services;
using Xunit;

namespace OmniSharp.Tests
{
    public class CodingActionsFacts
    {
        [Fact]
        public async Task Can_get_code_actions_from_nrefactory()
        {
            var source =
                @"public class Class1
                  {
                      public void Whatever()
                      {
                          int$ i = 1;
                      }
                  }";

            var refactorings = await FindRefactoringsAsync(source);
            Assert.Contains("Use 'var' keyword", refactorings);
        }

        [Fact]
        public async Task Can_get_code_actions_from_roslyn()
        {
            var source =
                  @"public class Class1
                    {
                        public void Whatever()
                        {
                            Conso$le.Write(""should be using System;"");
                        }
                    }";

            var refactorings = await FindRefactoringsAsync(source);
            Assert.Contains("using System;", refactorings);
        }

        [Fact]
        public async Task Can_sort_usings()
        {
            var source =
                  @"using MyNamespace3;
                    using MyNamespace4;
                    using MyNamespace2;
                    using System;
                    u$sing MyNamespace1;";

            var expected =
                  @"using System;
                    using MyNamespace1;
                    using MyNamespace2;
                    using MyNamespace3;
                    using MyNamespace4;";
                
            var newSource = await RunRefactoring(source, "Sort usings");
            Assert.Equal(expected, newSource);
        }

        [Fact]
        public async Task Can_remove_unnecessary_usings()
        {
            var source =
                  @"using MyNamespace3;
                    using MyNamespace4;
                    using MyNamespace2;
                    using System;
                    u$sing MyNamespace1;

public class c {public c() {Console.Write(1);}}";

            var expected =
                  @"using System;

public class c {public c() {Console.Write(1);}}";
                
            var newSource = await RunRefactoring(source, "Remove Unnecessary Usings");
            Assert.Equal(expected, newSource);
        }
        
        private async Task<string> RunRefactoring(string source, string refactoringName)
        {
            var refactorings = await FindRefactoringsAsync(source);
            Assert.Contains(refactoringName, refactorings);
            var index = refactorings.ToList().IndexOf(refactoringName);
            return await RunRefactoringsAsync(source, index);
        }

        private async Task<IEnumerable<string>> FindRefactoringsAsync(string source)
        {
            var request = CreateCodeActionRequest(source);
            var workspace = TestHelpers.CreateSimpleWorkspace(request.Buffer);
            var controller = new CodeActionController(workspace, new ICodeActionProvider[] { new RoslynCodeActionProvider(), new NRefactoryCodeActionProvider() });
            var response = await controller.GetCodeActions(request);
            return response.CodeActions;
        }

        private async Task<string> RunRefactoringsAsync(string source, int codeActionIndex)
        {
            var request = CreateCodeActionRequest(source, codeActionIndex);
            var workspace = TestHelpers.CreateSimpleWorkspace(request.Buffer);
            var controller = new CodeActionController(workspace, new ICodeActionProvider[] { new RoslynCodeActionProvider(), new NRefactoryCodeActionProvider() });
            var response = await controller.RunCodeAction(request);
            return response.Text;
        }

        private CodeActionRequest CreateCodeActionRequest(string source, int codeActionIndex = 0, string fileName = "dummy.cs")
        {
            var lineColumn = TestHelpers.GetLineAndColumnFromDollar(source);
            return new CodeActionRequest
            {
                Line = lineColumn.Line,
                Column = lineColumn.Column,
                FileName = fileName,
                Buffer = source.Replace("$", ""),
                CodeAction = codeActionIndex
            };
        }
    }
}
#endif
