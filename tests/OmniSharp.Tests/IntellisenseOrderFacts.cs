using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using Xunit;
using OmniSharp.Models;

namespace OmniSharp.Tests
{
    public class IntellisenseFacts
    {
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
            ContainsCompletions(completions.Take(2), "WindowLeft", "WriteLine");
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
            ContainsCompletions(completions.Take(1), "WriteLine");
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
            ContainsCompletions(completions, "myvar", "MyClass1");
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
            ContainsCompletions(completions, "MyClass1", "myvar");
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

        private async Task<IEnumerable<string>> FindCompletionsAsync(string source)
        {
            var workspace = TestHelpers.CreateSimpleWorkspace(source);
            var controller = new OmnisharpController(workspace, null);
            var request = CreateRequest(source);
            var response = await controller.AutoComplete(request);
            var completions = response as IEnumerable<AutoCompleteResponse>;
            return completions.Select(completion => completion.CompletionText);
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
            };
        }

        private static string GetPartialWord(string editorText)
        {
            MatchCollection matches = Regex.Matches(editorText, @"([a-zA-Z0-9_]*)\$");
            return matches[0].Groups[1].ToString();
        }
    }
}