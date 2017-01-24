using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Xunit;
using Xunit.Abstractions;

namespace OmniSharp.Roslyn.CSharp.Tests
{
    public class IntellisenseFacts : AbstractAutoCompleteTests
    {
        public IntellisenseFacts(ITestOutputHelper output)
            : base(output)
        {
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
    }
}
