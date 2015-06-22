#if DNX451
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using OmniSharp.Models;
using OmniSharp.Services;
using Xunit;

namespace OmniSharp.Tests
{
    public class CodingActionsFacts
    {
        private OmnisharpWorkspace _workspace;
        private string bufferPath = $"{Path.DirectorySeparatorChar}somepath{Path.DirectorySeparatorChar}buffer.cs";

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

            var response = await RunRefactoring(source, "Sort usings");
            Assert.Equal(expected, response.Text);
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

            var response = await RunRefactoring(source, "Remove Unnecessary Usings");
            AssertIgnoringIndent(expected, response.Text);
        }

        [Fact]
        public async Task Can_get_ranged_code_action()
        {
            var source =
            @"public class Class1
              {
                  public void Whatever()
                  {
                      $Console.Write(""should be using System;"");$
                  }
              }";

            var refactorings = await FindRefactoringsAsync(source);
            Assert.Contains("Extract Method", refactorings);
        }

        [Fact]
        public async Task Can_extract_method()
        {
            var source =
                @"public class Class1
                  {
                      public void Whatever()
                      {
                          $Console.Write(""should be using System;"");$
                      }
                  }";

            var expected =
                  @"public class Class1
                    {
                        public void Whatever()
                        {
                            NewMethod();
                        }

                        private static void NewMethod()
                        {
                            Console.Write(""should be using System;"");
                      }
                  }";

            var response = await RunRefactoring(source, "Extract Method");
            AssertIgnoringIndent(expected, response.Text);
        }

        [Fact]
        public async Task Can_create_a_class_with_a_new_method_in_adjacent_file()
        {
            var source =
                  @"namespace MyNamespace
                  public class Class1
                  {
                      public void Whatever()
                      {
                          MyNew$Class.DoSomething();
                      }
                  }";

            var response = await RunRefactoring(source, "Generate class for 'MyNewClass' in 'MyNamespace' (in new file)", true);
            var change = response.Changes.First();
            Assert.Equal($"{Path.DirectorySeparatorChar}somepath{Path.DirectorySeparatorChar}MyNewClass.cs", change.FileName);
            var expected =
              @"namespace MyNamespace
              {
                  internal class MyNewClass
                  {
                  }
              }";

            AssertIgnoringIndent(expected, change.Changes.First().NewText);
            source =
                @"namespace MyNamespace
                public class Class1
                {
                    public void Whatever()
                    {
                        MyNewClass.DoS$omething();
                    }
                }";

            response = await RunRefactoring(source, "Generate method 'MyNewClass.DoSomething'", true);
            expected =
              @"internal static void DoSomething()
                {
                    throw new NotImplementedException();
                }
              ";
            change = response.Changes.First();
            AssertIgnoringIndent(expected, change.Changes.First().NewText);
        }

        private void AssertIgnoringIndent(string expected, string actual)
        {
            Assert.Equal(TrimLines(expected), TrimLines(actual), false, true, true);
        }

        private string TrimLines(string source)
        {
            return string.Join("\n", source.Split('\n').Select(s => s.Trim()));
        }

        private async Task<RunCodeActionResponse> RunRefactoring(string source, string refactoringName, bool wantsChanges = false)
        {
            var refactorings = await FindRefactoringsAsync(source);
            Assert.Contains(refactoringName, refactorings);
            var index = refactorings.ToList().IndexOf(refactoringName);
            return await RunRefactoringsAsync(source, index, wantsChanges);
        }

        private async Task<IEnumerable<string>> FindRefactoringsAsync(string source)
        {
            var request = CreateCodeActionRequest(source);
            _workspace = _workspace ?? TestHelpers.CreateSimpleWorkspace(request.Buffer, bufferPath);
            var controller = new CodeActionController(_workspace, new ICodeActionProvider[] { new RoslynCodeActionProvider(), new NRefactoryCodeActionProvider() }, new FakeLoggerFactory());
            var response = await controller.GetCodeActions(request);
            return response.CodeActions;
        }

        private async Task<RunCodeActionResponse> RunRefactoringsAsync(string source, int codeActionIndex, bool wantsChanges = false)
        {
            var request = CreateCodeActionRequest(source, codeActionIndex, wantsChanges: wantsChanges);
            _workspace = _workspace ?? TestHelpers.CreateSimpleWorkspace(request.Buffer, bufferPath);
            var controller = new CodeActionController(_workspace, new ICodeActionProvider[] { new RoslynCodeActionProvider(), new NRefactoryCodeActionProvider() }, new FakeLoggerFactory());
            var response = await controller.RunCodeAction(request);
            return response;
        }

        private CodeActionRequest CreateCodeActionRequest(string source, int codeActionIndex = 0, bool wantsChanges = false)
        {
            var range = TestHelpers.GetRangeFromDollars(source);
            return new CodeActionRequest
            {
                Line = range.Start.Line,
                Column = range.Start.Column,
                SelectionStartColumn = range.Start.Column,
                SelectionStartLine = range.Start.Line,
                SelectionEndColumn = range.End.Column,
                SelectionEndLine = range.End.Line,
                FileName = bufferPath,
                Buffer = source.Replace("$", ""),
                CodeAction = codeActionIndex,
                WantsTextChanges = wantsChanges
            };
        }
    }
}
#endif
