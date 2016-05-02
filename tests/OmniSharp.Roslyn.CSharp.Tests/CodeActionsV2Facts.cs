using System.Collections.Generic;
using System.Composition.Hosting;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;
using OmniSharp.Models.V2;
using OmniSharp.Roslyn.CSharp.Services.CodeActions;
using OmniSharp.Roslyn.CSharp.Services.Refactoring.V2;
using OmniSharp.Services;
using OmniSharp.Tests;
using Xunit;

namespace OmniSharp.Roslyn.CSharp.Tests
{
    /*
        Test todo list:
        
        * Sort Using was removed with NRefactory
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
     */
    
    public class CodingActionsV2Facts : IClassFixture<RoslynTestFixture>
    {
        private readonly string BufferPath = $"{Path.DirectorySeparatorChar}somepath{Path.DirectorySeparatorChar}buffer.cs";

        private OmnisharpWorkspace _omnisharpWorkspace;
        private CompositionHost _pluginHost;
        private readonly RoslynTestFixture _fixture;

        public CodingActionsV2Facts(RoslynTestFixture fixture)
        {
            _fixture = fixture;
        }

        private CompositionHost PluginHost
        {
            get
            {
                if (_pluginHost == null)
                {
                    _pluginHost = TestHelpers.CreatePluginHost(
                        new Assembly[]
                        {
                            typeof(RoslynCodeActionProvider).GetTypeInfo().Assembly,
                            typeof(GetCodeActionsService).GetTypeInfo().Assembly
                        });
                }

                return _pluginHost;
            }
        }

        private async Task<OmnisharpWorkspace> GetOmniSharpWorkspace(Models.Request request)
        {
            if (_omnisharpWorkspace == null)
            {
                _omnisharpWorkspace = await TestHelpers.CreateSimpleWorkspace(
                    PluginHost,
                    request.Buffer,
                    BufferPath);
            }

            return _omnisharpWorkspace;
        }

        [Fact]
        public async Task Can_get_code_actions_from_roslyn()
        {
            var source =
                  @"public class Class1
                    {
                        public void Whatever()
                        {
                            Gu$id.NewGuid();
                        }
                    }";

            var refactorings = await FindRefactoringNamesAsync(source);
            Assert.Contains("using System;", refactorings);
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

                public class c {public c() {Guid.NewGuid();}}";

            var expected =
                @"using System;

                public class c {public c() {Guid.NewGuid();}}";

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

        [Fact(Skip = "Test is still broken because the removal of NRefactory.")]
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

            var response = await RunRefactoring(source, "Generate type", true);

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

        private async Task<RunCodeActionResponse> RunRefactoring(
            string source,
            string refactoringName,
            bool wantsChanges = false)
        {
            IEnumerable<OmniSharpCodeAction> refactorings = await FindRefactoringsAsync(source);
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
            var workspace = await GetOmniSharpWorkspace(request);
            var codeActions = CreateCodeActionProviders();

            var controller = new GetCodeActionsService(workspace, codeActions, _fixture.FakeLoggerFactory);
            var response = await controller.Handle(request);

            return response.CodeActions;
        }

        private async Task<RunCodeActionResponse> RunRefactoringsAsync(
            string source,
            string identifier,
            bool wantsChanges = false)
        {
            var request = CreateRunCodeActionRequest(source, identifier, wantsChanges);
            var workspace = await GetOmniSharpWorkspace(request);
            var codeActions = CreateCodeActionProviders();

            var controller = new RunCodeActionService(workspace, codeActions, _fixture.FakeLoggerFactory);
            var response = await controller.Handle(request);

            return response;
        }

        private GetCodeActionsRequest CreateGetCodeActionsRequest(string source)
        {
            var range = TestHelpers.GetRangeFromDollars(source);

            return new GetCodeActionsRequest
            {
                Line = range.Start.Line,
                Column = range.Start.Column,
                FileName = BufferPath,
                Buffer = source.Replace("$", ""),
                Selection = GetSelection(range)
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
                FileName = BufferPath,
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

        private IEnumerable<ICodeActionProvider> CreateCodeActionProviders()
        {
            var loader = _fixture.CreateAssemblyLoader(_fixture.FakeLogger);
            
            yield return new RoslynCodeActionProvider(loader);
        }
    }
}
