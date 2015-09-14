using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using OmniSharp.Models;
using OmniSharp.Options;
using OmniSharp.Roslyn.CSharp.Services.Intellisense;
using OmniSharp.Tests;
using Xunit;

namespace OmniSharp.Roslyn.CSharp.Tests
{
    public class IntellisenseFacts
    {
        [Fact]
        public async Task DisplayText_is_correct_for_property()
        {
            var source =
                @"public class Class1 {
                    public int Foo { get; set; }
                    public Class1()
                        {
                            Foo$
                        }
                    }";

            var request = CreateRequest(source);
            request.WantSnippet = true;

            var completions = await FindCompletionsAsync(source, request);
            ContainsCompletions(completions.Select(c => c.DisplayText).Take(1), "Foo");
        }

        [Fact]
        public async Task DisplayText_is_correct_for_variable()
        {
            var source =
                @"public class Class1 {
                    public Class1()
                        {
                            var foo = 1;
                            foo$
                        }
                    }";

            var request = CreateRequest(source);
            request.WantSnippet = true;

            var completions = await FindCompletionsAsync(source, request);
            ContainsCompletions(completions.Select(c => c.DisplayText).Take(1), "foo");
        }

        [Fact]
        public async Task DisplayText_matches_snippet_for_snippet_response()
        {
            var source =
                @"public class Class1 {
                    public Class1()
                        {
                            Foo$
                        }
                    public void Foo(int bar = 1)
                        {
                        }
                    }";

            var request = CreateRequest(source);
            request.WantSnippet = true;

            var completions = await FindCompletionsAsync(source, request);
            ContainsCompletions(completions.Select(c => c.DisplayText).Take(2), "Foo()", "Foo(int bar = 1)");
        }

        [Fact]
        public async Task DisplayText_matches_snippet_for_non_snippet_response()
        {
            var source =
                @"public class Class1 {
                    public Class1()
                        {
                            Foo$
                        }
                    public void Foo(int bar = 1)
                        {
                        }
                    }";

            var request = CreateRequest(source);
            request.WantSnippet = false;

            var completions = await FindCompletionsAsync(source, request);
            ContainsCompletions(completions.Select(c => c.DisplayText).Take(1), "Foo(int bar = 1)");
        }

        [Fact]
        public async Task Returns_camel_case_completions()
        {
            var source =
                @"public class Class1 {
                    public Class1()
                        {
                            System.Console.wl$
                        }
                    }";

            var completions = await FindCompletionsAsync(source);
            ContainsCompletions(completions.Select(c => c.CompletionText).Take(2), "WindowLeft", "WriteLine");
        }

        [Fact]
        public async Task Returns_sub_sequence_completions()
        {
            var source =
                @"public class Class1 {
                    public Class1()
                        {
                            System.Console.wln$
                        }
                    }";

            var completions = await FindCompletionsAsync(source);
            ContainsCompletions(completions.Select(c => c.CompletionText).Take(1), "WriteLine");
        }

        [Fact]
        public async Task Returns_method_header()
        {
            var source =
                @"public class Class1 {
                    public Class1()
                        {
                            System.Console.wln$
                        }
                    }";

            var completions = await FindCompletionsAsync(source);
            ContainsCompletions(completions.Select(c => c.MethodHeader).Take(1), "WriteLine()");
        }

        [Fact]
        public async Task Returns_variable_before_class()
        {
            var source =
                @"public class MyClass1 {

                    public MyClass1()
                        {
                            var myvar = 1;
                            my$
                        }
                    }";

            var completions = await FindCompletionsAsync(source);
            ContainsCompletions(completions.Select(c => c.CompletionText), "myvar", "MyClass1");
        }

        [Fact]
        public async Task Returns_class_before_variable()
        {
            var source =
                @"public class MyClass1 {

                    public MyClass1()
                        {
                            var myvar = 1;
                            My$
                        }
                    }";

            var completions = await FindCompletionsAsync(source);
            ContainsCompletions(completions.Select(c => c.CompletionText), "MyClass1", "myvar");
        }

        private void ContainsCompletions(IEnumerable<string> completions, params string[] expected)
        {
            var same = completions.SequenceEqual(expected);
            if (!same)
            {
                System.Console.Error.WriteLine("Expected");
                System.Console.Error.WriteLine("--------");

                foreach (var completion in expected)
                {
                    System.Console.WriteLine(completion);
                }

                System.Console.Error.WriteLine();
                System.Console.Error.WriteLine("Found");
                System.Console.Error.WriteLine("-----");

                foreach (var completion in completions)
                {
                    System.Console.WriteLine(completion);
                }
            }
            Assert.Equal(expected, completions);
        }

        private async Task<IEnumerable<AutoCompleteResponse>> FindCompletionsAsync(string source, AutoCompleteRequest request = null)
        {
            var workspace = await TestHelpers.CreateSimpleWorkspace(source);
            var controller = new IntellisenseService(workspace, new FormattingOptions());

            if (request == null)
            {
                request = CreateRequest(source);
            }

            var response = await controller.Handle(request);
            var completions = response as IEnumerable<AutoCompleteResponse>;
            return completions;
        }

        private AutoCompleteRequest CreateRequest(string source, string fileName = "dummy.cs")
        {
            var lineColumn = TestHelpers.GetLineAndColumnFromDollar(source);
            return new AutoCompleteRequest
            {
                Line = lineColumn.Line,
                Column = lineColumn.Column,
                FileName = fileName,
                Buffer = source.Replace("$", ""),
                WordToComplete = GetPartialWord(source),
                WantMethodHeader = true
            };
        }

        private static string GetPartialWord(string editorText)
        {
            MatchCollection matches = Regex.Matches(editorText, @"([a-zA-Z0-9_]*)\$");
            return matches[0].Groups[1].ToString();
        }
    }
}
