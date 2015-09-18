#if DNX451
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using OmniSharp.Models.V2;
using OmniSharp.Services;
using OmniSharp.Roslyn.CSharp.Services.Refactoring.V2;
using Xunit;
using OmniSharp.Roslyn.CSharp.Services.CodeActions;
using System.Reflection;
using System.Composition.Hosting;

namespace OmniSharp.Tests
{
    public class CodingActionsV2Facts
    {
        private OmnisharpWorkspace _workspace;
        private CompositionHost _host;
        private string bufferPath = $"{Path.DirectorySeparatorChar}somepath{Path.DirectorySeparatorChar}buffer.cs";

        // [Fact]
        // public async Task Can_get_code_actions_from_nrefactory()
        // {
        //     var source =
        //         @"public class Class1
        //           {
        //               public void Whatever()
        //               {
        //                   int$ i = 1;
        //               }
        //           }";
        //
        //     var refactorings = await FindRefactoringNamesAsync(source);
        //     Assert.Contains("Use 'var' keyword", refactorings);
        // }

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

            var refactorings = await FindRefactoringNamesAsync(source);
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
            Assert.Equal(expected, response.Changes.First().Buffer);
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
            AssertIgnoringIndent(expected, response.Changes.First().Buffer);
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

            var refactorings = await FindRefactoringNamesAsync(source);
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
            AssertIgnoringIndent(expected, response.Changes.First().Buffer);
        }

        // [Fact]
        // public async Task Can_create_a_class_with_a_new_method_in_adjacent_file()
        // {
        //     var source =
        //           @"namespace MyNamespace
        //           public class Class1
        //           {
        //               public void Whatever()
        //               {
        //                   MyNew$Class.DoSomething();
        //               }
        //           }";
        //
        //     var response = await RunRefactoring(source, "Generate class for 'MyNewClass' in 'MyNamespace' (in new file)", true);
        //     var change = response.Changes.First();
        //     Assert.Equal($"{Path.DirectorySeparatorChar}somepath{Path.DirectorySeparatorChar}MyNewClass.cs", change.FileName);
        //     var expected =
        //       @"namespace MyNamespace
        //       {
        //           internal class MyNewClass
        //           {
        //           }
        //       }";
        //
        //     AssertIgnoringIndent(expected, change.Changes.First().NewText);
        //     source =
        //         @"namespace MyNamespace
        //         public class Class1
        //         {
        //             public void Whatever()
        //             {
        //                 MyNewClass.DoS$omething();
        //             }
        //         }";
        //
        //     response = await RunRefactoring(source, "Generate method 'MyNewClass.DoSomething'", true);
        //     expected =
        //       @"internal static void DoSomething()
        //         {
        //             throw new NotImplementedException();
        //         }
        //       ";
        //     change = response.Changes.First();
        //     AssertIgnoringIndent(expected, change.Changes.First().NewText);
        // }

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
            Assert.Contains(refactoringName, refactorings.Select(a => a.Name));
            var identifier = refactorings.First(action => action.Name.Equals(refactoringName)).Identifier;
            return await RunRefactoringsAsync(source, identifier, wantsChanges);
        }

        private async Task<IEnumerable<string>> FindRefactoringNamesAsync(string source)
        {
            var codeActions = await FindRefactoringsAsync(source);
            return codeActions.Select(a => a.Name);
        }

        private async Task<IEnumerable<OmniSharpCodeAction>> FindRefactoringsAsync(string source)
        {
            var request = CreateGetCodeActionsRequest(source);
            _host = _host ?? TestHelpers.CreatePluginHost(new[] { typeof(RoslynCodeActionProvider).GetTypeInfo().Assembly, typeof(NRefactoryCodeActionProvider).GetTypeInfo().Assembly, typeof(GetCodeActionsService).GetTypeInfo().Assembly });
            _workspace = _workspace ?? await TestHelpers.CreateSimpleWorkspace(_host, request.Buffer, bufferPath);
            var controller = new GetCodeActionsService(_workspace, new ICodeActionProvider[] { new RoslynCodeActionProvider(), new NRefactoryCodeActionProvider() }, new FakeLoggerFactory());
            var response = await controller.Handle(request);
            return response.CodeActions;
        }

        private async Task<RunCodeActionResponse> RunRefactoringsAsync(string source, string identifier, bool wantsChanges = false)
        {
            var request = CreateRunCodeActionRequest(source, identifier, wantsChanges);
            _host = _host ?? TestHelpers.CreatePluginHost(new[] { typeof(RoslynCodeActionProvider).GetTypeInfo().Assembly, typeof(NRefactoryCodeActionProvider).GetTypeInfo().Assembly, typeof(GetCodeActionsService).GetTypeInfo().Assembly });
            _workspace = _workspace ?? await TestHelpers.CreateSimpleWorkspace(_host, request.Buffer, bufferPath);
            var controller = new RunCodeActionService(_workspace, new ICodeActionProvider[] { new RoslynCodeActionProvider(), new NRefactoryCodeActionProvider() }, new FakeLoggerFactory());
            var response = await controller.Handle(request);
            return response;
        }

        private GetCodeActionsRequest CreateGetCodeActionsRequest(string source)
        {
            var range = TestHelpers.GetRangeFromDollars(source);
            Range selection = GetSelection(range);

            return new GetCodeActionsRequest
            {
                Line = range.Start.Line,
                Column = range.Start.Column,
                FileName = bufferPath,
                Buffer = source.Replace("$", ""),
                Selection = selection
            };
        }

        private RunCodeActionRequest CreateRunCodeActionRequest(string source, string identifier, bool wantChanges)
        {
            var range = TestHelpers.GetRangeFromDollars(source);
            var selection = GetSelection(range);

            return new RunCodeActionRequest
            {
                Line = range.Start.Line,
                Column = range.Start.Column,
                Selection = selection,
                FileName = bufferPath,
                Buffer = source.Replace("$", ""),
                Identifier = identifier,
                WantsTextChanges = wantChanges
            };
        }

        private static Range GetSelection(TestHelpers.Range range)
        {
            Range selection = null;
            if (!range.IsEmpty)
            {
                var start = new Point { Line = range.Start.Line, Column = range.Start.Column };
                var end = new Point { Line = range.End.Line, Column = range.End.Column };
                selection = new Range { Start = start, End = end };
            }

            return selection;
        }

    }
}
#endif
