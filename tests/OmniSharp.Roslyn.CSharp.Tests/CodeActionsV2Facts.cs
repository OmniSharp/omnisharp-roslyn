using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using OmniSharp.Models;
using OmniSharp.Models.V2;
using OmniSharp.Models.V2.CodeActions;
using OmniSharp.Roslyn.CSharp.Services.Refactoring.V2;
using TestUtility;
using Xunit;
using Xunit.Abstractions;

namespace OmniSharp.Roslyn.CSharp.Tests
{
    public class CodeActionsV2Facts : AbstractCodeActionsTestFixture
    {
        public CodeActionsV2Facts(ITestOutputHelper output)
            : base(output)
        {
        }

        [Theory]
        [InlineData(true)]
        [InlineData(false)]
        public async Task Can_get_code_actions_from_roslyn(bool roslynAnalyzersEnabled)
        {
            const string code =
                @"public class Class1
                    {
                        public void Whatever()
                        {
                            Gu[||]id.NewGuid();
                        }
                    }";

            var refactorings = await FindRefactoringNamesAsync(code, roslynAnalyzersEnabled);
            Assert.Contains("using System;", refactorings);
        }

        [Theory]
        [InlineData(true)]
        [InlineData(false)]
        public async Task Can_get_code_actions_from_external_source(bool roslynAnalyzersEnabled)
        {
            const string code =
                @"
                    using System.Threading.Tasks;
                    public class Class1
                    {
                        public async Task Whatever()
                        {
                            awa[||]it FooAsync();
                        }

                        public Task FooAsync() => return Task.FromResult(0);
                    }";

            var configuration = new Dictionary<string, string>
            {
                { "RoslynExtensionsOptions:LocationPaths:0", TestAssets.Instance.TestBinariesFolder },
            };

            var refactorings = await FindRefactoringsAsync(code,
                TestHelpers.GetConfigurationDataWithAnalyzerConfig(roslynAnalyzersEnabled, existingConfiguration: configuration));

            Assert.NotEmpty(refactorings);
            Assert.Contains("Add ConfigureAwait(false)", refactorings.Select(x => x.Name));
        }

        [Theory]
        [InlineData(true)]
        [InlineData(false)]
        public async Task Can_remove_unnecessary_usings(bool roslynAnalyzersEnabled)
        {
            const string code =
                @"using MyNamespace3;
                using MyNamespace4;
                using MyNamespace2;
                using System;
                u[||]sing MyNamespace1;

                public class c {public c() {Guid.NewGuid();}}";

            const string expected =
                @"using System;

                public class c {public c() {Guid.NewGuid();}}";

            var response = await RunRefactoringAsync(code, "Remove Unnecessary Usings", isAnalyzersEnabled: roslynAnalyzersEnabled);
            AssertIgnoringIndent(expected, ((ModifiedFileResponse)response.Changes.First()).Buffer);
        }

        [Theory]
        [InlineData(true)]
        [InlineData(false)]
        public async Task Can_get_ranged_code_action(bool roslynAnalyzersEnabled)
        {
            const string code =
                @"public class Class1
                {
                    public void Whatever()
                    {
                        [|Console.Write(""should be using System;"");|]
                    }
                }";

            var refactorings = await FindRefactoringNamesAsync(code, roslynAnalyzersEnabled);
            Assert.Contains("Extract Method", refactorings);
        }

        [Theory]
        [InlineData(true)]
        [InlineData(false)]
        public async Task Returns_ordered_code_actions(bool roslynAnalyzersEnabled)
        {
            const string code =
                @"public class Class1
                {
                    public void Whatever()
                    {
                        [|Console.Write(""should be using System;"");|]
                    }
                }";

            var refactorings = await FindRefactoringNamesAsync(code, roslynAnalyzersEnabled);

            List<string> expected = roslynAnalyzersEnabled ? new List<string>
            {
                "Fix formatting",
                "using System;",
                "System.Console",
                "Generate variable 'Console' -> Generate property 'Class1.Console'",
                "Generate variable 'Console' -> Generate field 'Class1.Console'",
                "Generate variable 'Console' -> Generate read-only field 'Class1.Console'",
                "Generate variable 'Console' -> Generate local 'Console'",
                "Generate variable 'Console' -> Generate parameter 'Console'",
                "Generate type 'Console' -> Generate class 'Console' in new file",
                "Generate type 'Console' -> Generate class 'Console'",
                "Generate type 'Console' -> Generate nested class 'Console'",
                "Extract Method"
            } : new List<string>
            {
                "using System;",
                "System.Console",
                "Generate variable 'Console' -> Generate property 'Class1.Console'",
                "Generate variable 'Console' -> Generate field 'Class1.Console'",
                "Generate variable 'Console' -> Generate read-only field 'Class1.Console'",
                "Generate variable 'Console' -> Generate local 'Console'",
                "Generate variable 'Console' -> Generate parameter 'Console'",
                "Generate type 'Console' -> Generate class 'Console' in new file",
                "Generate type 'Console' -> Generate class 'Console'",
                "Generate type 'Console' -> Generate nested class 'Console'",
                "Extract Method"
            };

            Assert.Equal(expected, refactorings);
        }

        [Theory]
        [InlineData(true)]
        [InlineData(false)]
        public async Task Can_extract_method(bool roslynAnalyzersEnabled)
        {
            const string code =
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
            var response = await RunRefactoringAsync(code, "Extract Method", isAnalyzersEnabled: roslynAnalyzersEnabled);
            AssertIgnoringIndent(expected, ((ModifiedFileResponse)response.Changes.First()).Buffer);
        }

        [Theory]
        [InlineData(true)]
        [InlineData(false)]
        public async Task Can_generate_type_and_return_name_of_new_file(bool roslynAnalyzersEnabled)
        {
            using (var testProject = await TestAssets.Instance.GetTestProjectAsync("ProjectWithMissingType"))
            using (var host =  OmniSharpTestHost.Create(testProject.Directory, testOutput: TestOutput, configurationData: TestHelpers.GetConfigurationDataWithAnalyzerConfig(roslynAnalyzersEnabled)))
            {
                var requestHandler = host.GetRequestHandler<RunCodeActionService>(OmniSharpEndpoints.V2.RunCodeAction);
                var document = host.Workspace.CurrentSolution.Projects.First().Documents.First();
                var buffer = await document.GetTextAsync();
                var path = document.FilePath;

                var request = new RunCodeActionRequest
                {
                    Line = 8,
                    Column = 12,
                    FileName = path,
                    Buffer = buffer.ToString(),
                    Identifier = "Generate class 'Z' in new file",
                    WantsTextChanges = true,
                    WantsAllCodeActionOperations = true
                };

                var response = await requestHandler.Handle(request);
                var changes = response.Changes.ToArray();
                Assert.Equal(2, changes.Length);
                Assert.NotNull(changes[0].FileName);

                Assert.True(File.Exists(changes[0].FileName));
                Assert.Equal(@"namespace ConsoleApplication
{
    internal class Z
    {
    }
}".Replace("\r\n", "\n"), ((ModifiedFileResponse)changes[0]).Changes.First().NewText);

                Assert.NotNull(changes[1].FileName);
            }
        }

        [Theory]
        [InlineData(true)]
        [InlineData(false)]
        public async Task Can_send_rename_and_fileOpen_responses_when_codeAction_renames_file(bool roslynAnalyzersEnabled)
        {
            using (var testProject = await TestAssets.Instance.GetTestProjectAsync("ProjectWithMismatchedFileName"))
            using (var host = OmniSharpTestHost.Create(testProject.Directory, testOutput: TestOutput, configurationData: TestHelpers.GetConfigurationDataWithAnalyzerConfig(roslynAnalyzersEnabled)))
            {
                var requestHandler = host.GetRequestHandler<RunCodeActionService>(OmniSharpEndpoints.V2.RunCodeAction);
                var document = host.Workspace.CurrentSolution.Projects.First().Documents.First();
                var buffer = await document.GetTextAsync();
                var path = document.FilePath;

                var request = new RunCodeActionRequest
                {
                    Line = 4,
                    Column = 10,
                    FileName = path,
                    Buffer = buffer.ToString(),
                    Identifier = "Rename file to Class1.cs",
                    WantsTextChanges = true,
                    WantsAllCodeActionOperations = true
                };

                var response = await requestHandler.Handle(request);
                var changes = response.Changes.ToArray();
                Assert.Equal(2, changes.Length);
                Assert.Equal(FileModificationType.Renamed, changes[0].ModificationType);
                Assert.Contains("Class1.cs", ((RenamedFileResponse)changes[0]).NewFileName);
                Assert.False(File.Exists(((RenamedFileResponse)changes[0]).FileName), "The old renamed file exists - even though it should not.");
                Assert.True(File.Exists(((RenamedFileResponse)changes[0]).NewFileName), "The new renamed file doesn't exist - even though it should.");
                Assert.Equal(FileModificationType.Opened, changes[1].ModificationType);
            }
        }
    }
}
