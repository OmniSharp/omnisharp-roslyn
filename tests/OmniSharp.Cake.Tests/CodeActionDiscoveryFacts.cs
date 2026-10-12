using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using OmniSharp.Cake.Services.RequestHandlers.Buffer;
using OmniSharp.Cake.Services.RequestHandlers.Refactoring.V2;
using OmniSharp.Mef;
using OmniSharp.Models;
using OmniSharp.Models.UpdateBuffer;
using OmniSharp.Models.V2;
using OmniSharp.Models.V2.CodeActions;
using Roslyn.Test.Utilities;
using TestUtility;
using Xunit;
using Range = OmniSharp.Models.V2.Range;

namespace OmniSharp.Cake.Tests
{
    public class CodeActionDiscoveryFacts : CodeActionsTestBase
    {
        public CodeActionDiscoveryFacts(ITestOutputHelper output)
            : base(output)
        {
        }

        [Fact]
        public async Task Can_get_code_actions_from_roslyn()
        {
            const string code = "var regex = new Reg[||]ex();";

            var refactorings = await FindRefactoringNamesAsync(code);
            Assert.Contains(("using System.Text.RegularExpressions;", CodeActionKind.QuickFix), refactorings);
        }

        [Fact]
        public async Task Can_get_ranged_code_action()
        {
            const string code =
                @"public class Class1
                {
                    public void Whatever()
                    {
                        [|Console.Write(""should be using System;"");|]
                    }
                }";

            var refactorings = await FindRefactoringNamesAsync(code);
            Assert.Contains(("Extract method", CodeActionKind.RefactorExtract), refactorings);
        }

        [Fact]
        public async Task Returns_ordered_code_actions()
        {
            const string code =
                @"public class Class1
                {
                    public void Whatever()
                    {
                        [|Regex.Match(""foo"", ""bar"");|]
                    }
                }";

            var refactorings = await FindRefactoringNamesAsync(code);
            var expected = new List<(string, string CodeActionKind)>
            {
                ("using System.Text.RegularExpressions;", CodeActionKind.QuickFix),
                ("System.Text.RegularExpressions.Regex", CodeActionKind.QuickFix),
                ("Extract method", CodeActionKind.RefactorExtract),
                ("Extract local function", CodeActionKind.RefactorExtract),
                ("Use expression body for method", CodeActionKind.Refactor),
                ("Introduce local for 'Regex.Match(\"foo\", \"bar\")'", CodeActionKind.Refactor),
                ("Introduce parameter for 'Regex.Match(\"foo\", \"bar\")' -> and update call sites directly", CodeActionKind.Refactor),
                ("Introduce parameter for 'Regex.Match(\"foo\", \"bar\")' -> into extracted method to invoke at call sites", CodeActionKind.Refactor),
                ("Introduce parameter for 'Regex.Match(\"foo\", \"bar\")' -> into new overload", CodeActionKind.Refactor),
                ("Introduce parameter for all occurrences of 'Regex.Match(\"foo\", \"bar\")' -> and update call sites directly", CodeActionKind.Refactor),
                ("Introduce parameter for all occurrences of 'Regex.Match(\"foo\", \"bar\")' -> into extracted method to invoke at call sites", CodeActionKind.Refactor),
                ("Introduce parameter for all occurrences of 'Regex.Match(\"foo\", \"bar\")' -> into new overload", CodeActionKind.Refactor)
            };
            AssertEx.Equal(expected, refactorings);
        }

        [Fact]
        public async Task Should_Not_Find_Rename_File()
        {
            const string code =
                @"public class Class[||]1
                {
                    public void Whatever()
                    {
                        Console.Write(""should be using System;"");
                    }
                }";

            var refactorings = await FindRefactoringNamesAsync(code);
            Assert.DoesNotContain(refactorings, x => x.Name.StartsWith("Rename file to"));
        }
    }
}
