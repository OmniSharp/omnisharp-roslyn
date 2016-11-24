using System;
using System.Collections.Generic;
using System.Composition.Hosting;
using System.Linq;
using System.Reflection;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
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
            _plugInHost = TestHelpers.CreatePluginHost(new[] { typeof(IntellisenseService).GetTypeInfo().Assembly });
        }

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

            var request = CreateRequest(source, wantSnippet: true);
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

            var request = CreateRequest(source, wantSnippet: true);
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

            var request = CreateRequest(source, wantSnippet: true);
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

            var request = CreateRequest(source, wantSnippet: false);
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
                            System.Guid.tp$
                        }
                    }";

            var completions = await FindCompletionsAsync(source);

            ContainsCompletions(completions.Select(c => c.CompletionText).Take(1), "TryParse");
        }

        [Fact]
        public async Task Returns_sub_sequence_completions()
        {
            var source =
                @"public class Class1 {
                    public Class1()
                        {
                            System.Guid.ng$
                        }
                    }";

            var completions = await FindCompletionsAsync(source);

            ContainsCompletions(completions.Select(c => c.CompletionText).Take(1), "NewGuid");
        }

        [Fact]
        public async Task Returns_method_header()
        {
            var source =
                @"public class Class1 {
                    public Class1()
                        {
                            System.Guid.ng$
                        }
                    }";

            var completions = await FindCompletionsAsync(source);

            ContainsCompletions(completions.Select(c => c.MethodHeader).Take(1), "NewGuid()");
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

        [Fact]
        public async Task Returns_empty_sequence_in_invalid_context()
        {
            var source =
                @"public class MyClass1 {

                    public MyClass1()
                        {
                            var x$
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

        private async Task<IEnumerable<AutoCompleteResponse>> FindCompletionsAsync(string source, AutoCompleteRequest request = null)
        {
            var workspace = await TestHelpers.CreateSimpleWorkspace(_plugInHost, source);
            var controller = new IntellisenseService(workspace, new FormattingOptions());

            if (request == null)
            {
                request = CreateRequest(source);
            }

            return await controller.Handle(request);
        }

        private AutoCompleteRequest CreateRequest(string source, string fileName = "dummy.cs", bool wantSnippet = false)
        {
            var lineColumn = TestHelpers.GetLineAndColumnFromDollar(source);

            return new AutoCompleteRequest
            {
                Line = lineColumn.Line,
                Column = lineColumn.Column,
                FileName = fileName,
                Buffer = source.Replace("$", ""),
                WordToComplete = GetPartialWord(source),
                WantMethodHeader = true,
                WantSnippet = wantSnippet
            };
        }

        private static string GetPartialWord(string editorText)
        {
            MatchCollection matches = Regex.Matches(editorText, @"([a-zA-Z0-9_]*)\$");
            return matches[0].Groups[1].ToString();
        }
    }
}
