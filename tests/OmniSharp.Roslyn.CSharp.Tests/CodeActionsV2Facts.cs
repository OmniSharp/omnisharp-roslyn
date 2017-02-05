using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;
using OmniSharp.Models.V2;
using OmniSharp.Roslyn.CSharp.Services;
using OmniSharp.Roslyn.CSharp.Services.CodeActions;
using OmniSharp.Roslyn.CSharp.Services.Refactoring.V2;
using OmniSharp.Services;
using TestUtility;
using Xunit;
using Xunit.Abstractions;

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

    public class CodingActionsV2Facts : AbstractTestFixture
    {
        private readonly string BufferPath = $"{Path.DirectorySeparatorChar}somepath{Path.DirectorySeparatorChar}buffer.cs";

        public CodingActionsV2Facts(ITestOutputHelper output)
            : base(output)
        {
        }

        protected override IEnumerable<Assembly> GetHostAssemblies()
        {
            yield return GetAssembly<RoslynCodeActionProvider>();
            yield return GetAssembly<GetCodeActionsService>();
        }

        [Fact]
        public async Task Can_get_code_actions_from_roslyn()
        {
            const string source =
                  @"public class Class1
                    {
                        public void Whatever()
                        {
                            Gu[||]id.NewGuid();
                        }
                    }";

            var refactorings = await FindRefactoringNamesAsync(source);
            Assert.Contains("using System;", refactorings);
        }

        [Fact]
        public async Task Can_remove_unnecessary_usings()
        {
            const string source =
                @"using MyNamespace3;
                using MyNamespace4;
                using MyNamespace2;
                using System;
                u[||]sing MyNamespace1;

                public class c {public c() {Guid.NewGuid();}}";

            const string expected =
                @"using System;

                public class c {public c() {Guid.NewGuid();}}";

            var response = await RunRefactoring(source, "Remove Unnecessary Usings");
            AssertIgnoringIndent(expected, response.Changes.First().Buffer);
        }

        [Fact]
        public async Task Can_get_ranged_code_action()
        {
            const string source =
                @"public class Class1
                  {
                      public void Whatever()
                      {
                          [|Console.Write(""should be using System;"");|]
                      }
                  }";

            var refactorings = await FindRefactoringNamesAsync(source);
            Assert.Contains("Extract Method", refactorings);
        }

        [Fact]
        public async Task Can_extract_method()
        {
            const string source =
                @"public class Class1
                  {
                      public void Whatever()
                      {
                          [|Console.Write(""should be using System;"");|]
                      }
                  }";

            const string expected =
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
                        MyNew[||]Class.DoSomething();
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
            var testFile = new TestFile(BufferPath, source);
            var span = testFile.Content.GetSpans().Single();
            var range = testFile.Content.GetRangeFromSpan(span);

            var request = new GetCodeActionsRequest
            {
                Line = range.Start.Line,
                Column = range.Start.Offset,
                FileName = BufferPath,
                Buffer = testFile.Content.Code,
                Selection = GetSelection(range)
            };

            var workspace = await CreateWorkspaceAsync(testFile);
            var helper = new CodeActionHelper(this.AssemblyLoader);
            var providers = CreateCodeActionProviders();

            var controller = new GetCodeActionsService(workspace, helper, providers, this.LoggerFactory);
            var response = await controller.Handle(request);

            return response.CodeActions;
        }

        private async Task<RunCodeActionResponse> RunRefactoringsAsync(
            string source,
            string identifier,
            bool wantsChanges = false)
        {
            var testFile = new TestFile(BufferPath, source);
            var span = testFile.Content.GetSpans().Single();
            var range = testFile.Content.GetRangeFromSpan(span);

            var request = new RunCodeActionRequest
            {
                Line = range.Start.Line,
                Column = range.Start.Offset,
                Selection = GetSelection(range),
                FileName = BufferPath,
                Buffer = testFile.Content.Code,
                Identifier = identifier,
                WantsTextChanges = wantsChanges
            };

            var workspace = await CreateWorkspaceAsync(testFile);
            var helper = new CodeActionHelper(this.AssemblyLoader);
            var providers = CreateCodeActionProviders();

            var controller = new RunCodeActionService(workspace, helper, providers, this.LoggerFactory);
            var response = await controller.Handle(request);

            return response;
        }

        private static Range GetSelection(TextRange range)
        {
            if (range.IsEmpty)
            {
                return null;
            }

            return new Range
            {
                Start = new Point { Line = range.Start.Line, Column = range.Start.Offset },
                End = new Point { Line = range.End.Line, Column = range.End.Offset }
            };
        }

        private IEnumerable<ICodeActionProvider> CreateCodeActionProviders()
        {
            var hostServicesProvider = new RoslynFeaturesHostServicesProvider(this.AssemblyLoader);

            yield return new RoslynCodeActionProvider(hostServicesProvider);
        }
    }
}
