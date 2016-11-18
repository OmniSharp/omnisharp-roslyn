using System;
using System.Collections.Generic;
using System.Composition.Hosting;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;
using Microsoft.CodeAnalysis.Text;
using OmniSharp.Models;
using OmniSharp.Options;
using OmniSharp.Roslyn.CSharp.Services.Intellisense;
using TestUtility;
using Xunit;

namespace OmniSharp.Roslyn.CSharp.Tests
{
    public class IntellisenseFacts
    {
        private CompositionHost _plugInHost;

        public IntellisenseFacts()
        {
            _plugInHost = TestHelpers.CreatePluginHost(new[]
            {
                typeof(IntellisenseService).GetTypeInfo().Assembly
            });
        }

        [Fact]
        public async Task DisplayText_is_correct_for_property()
        {
            const string input =
                @"public class Class1 {
                    public int Foo { get; set; }
                    public Class1()
                        {
                            Foo$$
                        }
                    }";

            var request = CreateRequest(input, wantSnippet: true);
            var completions = await FindCompletionsAsync(input, request);

            ContainsCompletions(completions.Select(c => c.DisplayText).Take(1), "Foo");
        }

        [Fact]
        public async Task DisplayText_is_correct_for_variable()
        {
            const string input =
                @"public class Class1 {
                    public Class1()
                        {
                            var foo = 1;
                            foo$$
                        }
                    }";

            var request = CreateRequest(input, wantSnippet: true);
            var completions = await FindCompletionsAsync(input, request);

            ContainsCompletions(completions.Select(c => c.DisplayText).Take(1), "foo");
        }

        [Fact]
        public async Task DisplayText_matches_snippet_for_snippet_response()
        {
            const string input =
                @"public class Class1 {
                    public Class1()
                        {
                            Foo$$
                        }
                    public void Foo(int bar = 1)
                        {
                        }
                    }";

            var request = CreateRequest(input, wantSnippet: true);
            var completions = await FindCompletionsAsync(input, request);

            ContainsCompletions(completions.Select(c => c.DisplayText).Take(2), "Foo()", "Foo(int bar = 1)");
        }

        [Fact]
        public async Task DisplayText_matches_snippet_for_non_snippet_response()
        {
            const string input =
                @"public class Class1 {
                    public Class1()
                        {
                            Foo$$
                        }
                    public void Foo(int bar = 1)
                        {
                        }
                    }";

            var request = CreateRequest(input, wantSnippet: false);
            var completions = await FindCompletionsAsync(input, request);

            ContainsCompletions(completions.Select(c => c.DisplayText).Take(1), "Foo(int bar = 1)");
        }

        [Fact]
        public async Task Returns_camel_case_completions()
        {
            const string input =
                @"public class Class1 {
                    public Class1()
                        {
                            System.Guid.tp$$
                        }
                    }";

            var completions = await FindCompletionsAsync(input);

            ContainsCompletions(completions.Select(c => c.CompletionText).Take(1), "TryParse");
        }

        [Fact]
        public async Task Returns_sub_sequence_completions()
        {
            const string input =
                @"public class Class1 {
                    public Class1()
                        {
                            System.Guid.ng$$
                        }
                    }";

            var completions = await FindCompletionsAsync(input);

            ContainsCompletions(completions.Select(c => c.CompletionText).Take(1), "NewGuid");
        }

        [Fact]
        public async Task Returns_method_header()
        {
            const string input =
                @"public class Class1 {
                    public Class1()
                        {
                            System.Guid.ng$$
                        }
                    }";

            var completions = await FindCompletionsAsync(input);

            ContainsCompletions(completions.Select(c => c.MethodHeader).Take(1), "NewGuid()");
        }

        [Fact]
        public async Task Returns_variable_before_class()
        {
            const string input =
                @"public class MyClass1 {

                    public MyClass1()
                        {
                            var myvar = 1;
                            my$$
                        }
                    }";

            var completions = await FindCompletionsAsync(input);

            ContainsCompletions(completions.Select(c => c.CompletionText), "myvar", "MyClass1");
        }

        [Fact]
        public async Task Returns_class_before_variable()
        {
            const string input =
                @"public class MyClass1 {

                    public MyClass1()
                        {
                            var myvar = 1;
                            My$$
                        }
                    }";

            var completions = await FindCompletionsAsync(input);

            ContainsCompletions(completions.Select(c => c.CompletionText), "MyClass1", "myvar");
        }

        [Fact]
        public async Task Returns_empty_sequence_in_invalid_context()
        {
            const string source =
                @"public class MyClass1 {

                    public MyClass1()
                        {
                            var x$$
                        }
                    }";

            var completions = await FindCompletionsAsync(source);
            ContainsCompletions(completions.Select(c => c.CompletionText), Array.Empty<string>());
        }

        private void ContainsCompletions(IEnumerable<string> completions, params string[] expected)
        {
            if (!completions.SequenceEqual(expected))
            {
                Console.Error.WriteLine("Expected");
                Console.Error.WriteLine("--------");

                foreach (var completion in expected)
                {
                    Console.WriteLine(completion);
                }

                Console.Error.WriteLine();
                Console.Error.WriteLine("Found");
                Console.Error.WriteLine("-----");

                foreach (var completion in completions)
                {
                    Console.WriteLine(completion);
                }
            }

            Assert.Equal(expected, completions.ToArray());
        }

        private async Task<IEnumerable<AutoCompleteResponse>> FindCompletionsAsync(string input, AutoCompleteRequest request = null)
        {
            var markup = MarkupCode.Parse(input);

            var workspace = await TestHelpers.CreateSimpleWorkspace(_plugInHost, markup.Code);
            var controller = new IntellisenseService(workspace, new FormattingOptions());

            if (request == null)
            {
                request = CreateRequest(input);
            }

            return await controller.Handle(request);
        }

        private AutoCompleteRequest CreateRequest(string input, string fileName = "dummy.cs", bool wantSnippet = false)
        {
            var markup = MarkupCode.Parse(input);

            var text = SourceText.From(markup.Code);
            var line = text.Lines.GetLineFromPosition(markup.Position);

            return new AutoCompleteRequest
            {
                Line = line.LineNumber,
                Column = markup.Position - line.Start,
                FileName = fileName,
                Buffer = markup.Code,
                WordToComplete = GetPartialWord(markup),
                WantMethodHeader = true,
                WantSnippet = wantSnippet
            };
        }

        private static string GetPartialWord(MarkupCode markup)
        {
            if (!markup.HasPosition || markup.Position == 0)
            {
                return string.Empty;
            }

            var index = markup.Position;
            while (index >= 1)
            {
                var ch = markup.Code[index - 1];
                if (ch != '_' && !char.IsLetterOrDigit(ch))
                {
                    break;
                }

                index--;
            }

            return markup.Code.Substring(index, markup.Position - index);
        }
    }
}
