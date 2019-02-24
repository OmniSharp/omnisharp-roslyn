using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
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
    public class CodeActionsWithOptionsFacts : AbstractTestFixture
    {
        private readonly string BufferPath = $"{Path.DirectorySeparatorChar}somepath{Path.DirectorySeparatorChar}buffer.cs";

        public CodeActionsWithOptionsFacts(ITestOutputHelper output)
            : base(output)
        {
        }

        [Fact]
        public async Task Can_generate_constructor_with_default_arguments()
        {
            const string code =
                @"public class Class1[||]
                {
                    public string PropertyHere { get; set; }
                }";
            const string expected =
                @"public class Class1
                {
                    public Class1(string propertyHere)
                    {
                        PropertyHere = propertyHere;
                    }

                    public string PropertyHere { get; set; }
                }
                ";
            var response = await RunRefactoringAsync(code, "Generate constructor...");
            AssertIgnoringIndent(expected, ((ModifiedFileResponse)response.Changes.First()).Buffer);
        }

        [Fact]
        public async Task Can_generate_overrides()
        {
            const string code =
                @"public class Class1[||]
                {
                }";
            const string expected =
                @"public class Class1
                {
                    public override bool Equals(object obj)
                    {
                        return base.Equals(obj);
                    }

                    public override int GetHashCode()
                    {
                        return base.GetHashCode();
                    }

                    public override string ToString()
                    {
                        return base.ToString();
                    }
                }
                ";
            var response = await RunRefactoringAsync(code, "Generate overrides...");
            AssertIgnoringIndent(expected, ((ModifiedFileResponse)response.Changes.First()).Buffer);
        }

        [Fact]
        public async Task Can_generate_equals_for_object()
        {
            const string code =
                @"public class Class1[||]
                {
                    public string PropertyHere { get; set; }
                }";
            const string expected =
                @"public class Class1
                {
                    public string PropertyHere { get; set; }

                    public override bool Equals(object obj)
                    {
                        return obj is Class1 @class &&
                               PropertyHere == @class.PropertyHere;
                    }
                }
                ";
            var response = await RunRefactoringAsync(code, "Generate Equals(object)...");
            AssertIgnoringIndent(expected, ((ModifiedFileResponse)response.Changes.First()).Buffer);
        }

        [Fact]
        public async Task Can_generate_equals_and_hashcode_for_object()
        {
            const string code =
                @"public class Class1[||]
                {
                    public string PropertyHere { get; set; }
                }";

            // Be aware that if used with newer .NET framework than omnisharp uses (4.6).
            // this will result more modern result with HashCode.Combine(PropertyHere);
            const string expected =
                @"
                using System.Collections.Generic;
                public class Class1
                {
                    public string PropertyHere { get; set; }
                    public override bool Equals(object obj)
                    {
                        return obj is Class1 @class &&
                           PropertyHere == @class.PropertyHere;
                    }
                    public override int GetHashCode()
                    {
                        return 1887327142 + EqualityComparer<string>.Default.GetHashCode(PropertyHere);
                    }
                }
                ";
            var response = await RunRefactoringAsync(code, "Generate Equals and GetHashCode...");

            AssertIgnoringIndent(expected, ((ModifiedFileResponse)response.Changes.First()).Buffer);
        }

        [Fact]
        // How to implement this witout proper UI?
        public async Task Blacklists_Change_signature()
        {
            const string code =
                @"public class Class1
                {
                    public void Foo(string a[||], string b)
                    {
                    }
                }";

            var response = await FindRefactoringNamesAsync(code);

            Assert.DoesNotContain("Change signature...", response);
        }

        [Fact]
        // Theres generate type without options that gives ~same result with default ui. No point to bring this.
        public async Task Blacklists_generate_type_with_UI()
        {
            const string code =
                @"public class Class1: NonExistentBaseType[||]
                {
                }";

            var response = await FindRefactoringNamesAsync(code);

            Assert.DoesNotContain("Generate type 'NonExistentBaseType' -> Generate new type...", response);
        }

        [Fact]
        // Theres generate type without options that gives ~same result with default ui. No point to bring this.
        public async Task Blacklists_pull_members_up_with_UI()
        {
            const string code =
                @"
                public class Class1: BaseClass
                {
                    public string Foo[||] { get; set; }
                }

                public class BaseClass {}
";

            var response = await FindRefactoringNamesAsync(code);

            Assert.DoesNotContain("Pull 'Foo' up -> Pull members up to base type...", response);
        }

        [Fact(Skip = "This feature isn't available before roslyn analyzers are available, extract interface is action to one of analysis.")]
        public async Task Can_extract_interface()
        {
            const string code =
                @"public class Class1[||]
                {
                    public string PropertyHere { get; set; }
                }";

            const string expected =
                @"
                public interface IClass1
                {
                    string PropertyHere { get; set; }
                }

                public class Class1
                {
                    public string PropertyHere { get; set; }
                }
                ";
            var response = await RunRefactoringAsync(code, "Extract interface...");

            AssertIgnoringIndent(expected, ((ModifiedFileResponse)response.Changes.First()).Buffer);
        }

        [Fact]
        public void Check_better_alternative_available_for_codeaction_with_options()
        {
            // This keeps record is Microsoft.CodeAnalysis.PickMembers.IPickMembersService public or not
            // and should fail on case where interface is exposed. Which likely means that this is officially
            // supported scenario by roslyn.
            //
            // In case it's exposed by roslyn team these services can be simplified.
            // Steps are likely following:
            // - Remove Castle.Core (proxies not needed)
            // - Replace ExportWorkspaceServiceFactoryWithAssemblyQualifiedName with Microsoft.CodeAnalysis.Host.Mef.ExportWorkspaceServiceAttribute
            // - Fix proxy classes to implement IPickMembersService / IExtractInterfaceOptionsService ... instead of proxy and reflection.
            // - Remove all factories using ExportWorkspaceServiceFactoryWithAssemblyQualifiedName and factory itself.
            // Following issue may have additional information: https://github.com/dotnet/roslyn/issues/33277
            var pickMemberServiceType = Assembly.Load("Microsoft.CodeAnalysis.Features").GetType("Microsoft.CodeAnalysis.PickMembers.IPickMembersService");
            Assert.False(pickMemberServiceType.IsPublic);
        }

        private static void AssertIgnoringIndent(string expected, string actual)
        {
            Assert.Equal(TrimLines(expected), TrimLines(actual), false, true, true);
        }

        private static string TrimLines(string source)
        {
            return string.Join("\n", source.Split('\n').Select(s => s.Trim())).Replace("\n","").Replace("\r","");
        }

        private async Task<RunCodeActionResponse> RunRefactoringAsync(string code, string refactoringName, bool wantsChanges = false)
        {
            var refactorings = await FindRefactoringsAsync(code);
            Assert.Contains(refactoringName, refactorings.Select(a => a.Name));

            var identifier = refactorings.First(action => action.Name.Equals(refactoringName)).Identifier;
            return await RunRefactoringsAsync(code, identifier, wantsChanges);
        }

        private async Task<IEnumerable<string>> FindRefactoringNamesAsync(string code)
        {
            var codeActions = await FindRefactoringsAsync(code);

            return codeActions.Select(a => a.Name);
        }

        private async Task<IEnumerable<OmniSharpCodeAction>> FindRefactoringsAsync(string code, IDictionary<string, string> configurationData = null)
        {
            var testFile = new TestFile(BufferPath, code);

            using (var host = CreateOmniSharpHost(new[] { testFile }, configurationData))
            {
                var requestHandler = host.GetRequestHandler<GetCodeActionsService>(OmniSharpEndpoints.V2.GetCodeActions);

                var span = testFile.Content.GetSpans().Single();
                var range = testFile.Content.GetRangeFromSpan(span);

                var request = new GetCodeActionsRequest
                {
                    Line = range.Start.Line,
                    Column = range.Start.Offset,
                    FileName = BufferPath,
                    Buffer = testFile.Content.Code,
                    Selection = GetSelection(range),
                };

                var response = await requestHandler.Handle(request);

                return response.CodeActions;
            }
        }

        private async Task<RunCodeActionResponse> RunRefactoringsAsync(string code, string identifier, bool wantsChanges = false)
        {
            var testFile = new TestFile(BufferPath, code);

            using (var host = CreateOmniSharpHost(testFile))
            {
                var requestHandler = host.GetRequestHandler<RunCodeActionService>(OmniSharpEndpoints.V2.RunCodeAction);

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
                    WantsTextChanges = wantsChanges,
                    WantsAllCodeActionOperations = true
                };

                return await requestHandler.Handle(request);
            }
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
    }
}
