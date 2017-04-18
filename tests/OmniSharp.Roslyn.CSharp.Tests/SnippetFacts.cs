using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using OmniSharp.Models.AutoComplete;
using Xunit;
using Xunit.Abstractions;

namespace OmniSharp.Roslyn.CSharp.Tests
{
    public class SnippetFacts : AbstractAutoCompleteTestFixture
    {
        private readonly ILogger _logger;

        public SnippetFacts(ITestOutputHelper output)
            : base(output)
        {
            this._logger = this.LoggerFactory.CreateLogger<SnippetFacts>();
        }

        [Fact]
        public async Task Can_template_generic_type_argument()
        {
            const string source =
                @"public class Class1 {
                    public Class1()
                        {
                            var l = new System.Collections.Generic.Lis$$
                        }
                    }";

            var completions = await FindCompletionsAsync(source, wantSnippet: true);
            ContainsSnippet("List<${1:T}>()$0", completions);
        }

        [Fact]
        public async Task Can_return_method_type_arguments_snippets()
        {
            const string source =
                @"using System.Collections.Generic;

                     public class Test {
                         public string Get<SomeType>()
                         {
                         }
                     }
                     public class Class1 {
                         public Class1()
                         {
                             var someObj = new Test();
                             someObj.G$$
                         }
                     }";

            var completions = await FindCompletionsAsync(source, wantSnippet: true);
            ContainsSnippet("Get<${1:SomeType}>()$0 : string", completions);
        }

        [Fact]
        public async Task Does_not_include_tsource_argument_type()
        {
            const string source =
                @"using System.Collections.Generic;
                using System.Linq;
                public class Class1 {
                    public Class1()
                    {
                        var l = new List<string>();
                        l.Firs$$
                    }
                }";

            var completions = await FindCompletionsAsync(source, wantSnippet: true);
            ContainsSnippet("First()$0 : string", completions);
            ContainsSnippet("FirstOrDefault(${1:Func<string, bool> predicate})$0 : string", completions);
        }

        [Fact]
        public async Task Does_not_include_tresult_argument_type()
        {
            const string source =
                @"using System.Collections.Generic;
                     using System.Linq;
                     public class Class1 {
                         public Class1()
                         {
                             var dict = new Dictionary<string, object>();
                             dict.Sel$$
                         }
                     }";

            var completions = await FindCompletionsAsync(source, wantSnippet: true);
            ContainsSnippet("Select(${1:Func<KeyValuePair<string, object>, TResult> selector})$0 : IEnumerable<TResult>", completions);
        }

        [Fact]
        public async Task Can_template_field()
        {
            const string source =
                @"using System.Collections.Generic;

                 public class Class1 {
                     public int someField;
                     public Class1()
                     {
                         somef$$
                     }
                 }";

            var completions = await FindCompletionsAsync(source, wantSnippet: true);
            ContainsSnippet("someField$0 : int", completions);
        }

        [Fact]
        public async Task Can_return_all_constructors()
        {
            const string source =
                @"public class MyClass {
                    public MyClass() {}
                    public MyClass(int param) {}
                    public MyClass(int param, string param) {}
                }

                public class Class2 {
                    public Class2()
                    {
                        var c = new My$$
                    }
                }";

            var completions = await FindCompletionsAsync(source, wantSnippet: true);
            ContainsSnippet("MyClass()$0", completions);
            ContainsSnippet("MyClass(${1:int param})$0", completions);
            ContainsSnippet("MyClass(${1:int param}, ${2:string param})$0", completions);
        }


        [Fact]
        public async Task Can_template_generic_type_arguments()
        {
            const string source =
                @"using System.Collections.Generic;
                  public class Class1 {
                      public Class1()
                      {
                          var l = new Dict$$
                      }
                  }";

            var completions = await FindCompletionsAsync(source, wantSnippet: true);
            ContainsSnippet("Dictionary<${1:TKey}, ${2:TValue}>()$0", completions);
        }

        [Fact]
        public async Task Can_template_parameter()
        {
            const string source =
                @"using System.Collections.Generic;
                  public class Class1 {
                      public Class1()
                      {
                          var l = new Lis$$
                      }
                  }";

            var completions = await FindCompletionsAsync(source, wantSnippet: true);
            ContainsSnippet("List<${1:T}>(${2:IEnumerable<T> collection})$0", completions);

        }

        [Fact]
        public async Task Can_complete_namespace()
        {
            const string source = "using Sys$$";

            var completions = await FindCompletionsAsync(source, wantSnippet: true);
            ContainsSnippet("System$0", completions);
        }

        [Fact]
        public async Task Can_complete_variable()
        {
            const string source = @"
                public class Class1
                {
                    public Class1()
                    {
                        var aVariable = 1;
                        av$$
                    }
                }
            ";

            var completions = await FindCompletionsAsync(source, wantSnippet: true);
            ContainsSnippet("aVariable$0 : int", completions);
        }

        [Fact]
        public async Task Void_methods_end_with_semicolons()
        {
            const string source = @"
                using System;
                public class Class1
                {
                    public Class1()
                    {
                        Array.Sor$$
                    }
                }
            ";

            var completions = await FindCompletionsAsync(source, wantSnippet: true);
            ContainsSnippet("Sort(${1:Array array});$0 : void", completions);
        }

        [Fact]
        public async Task Fuzzy_matches_are_returned_when_first_letters_match()
        {
            const string source = @"
                using System;
                public class Class1
                {
                    public Class1()
                    {
                        Guid.nwg$$
                    }
                }
            ";

            var completions = await FindCompletionsAsync(source, wantSnippet: true);
            ContainsSnippet("NewGuid()$0 : Guid", completions);
        }

        [Fact]
        public async Task Fuzzy_matches_are_not_returned_when_first_letters_do_not_match()
        {
            const string source = @"
                using System;
                public class Class1
                {
                    public Class1()
                    {
                        Console.rl$$
                    }
                }
            ";

            var completions = await FindCompletionsAsync(source, wantSnippet: true);
            var snippetTexts = GetSnippetTexts(completions);
            Assert.DoesNotContain("WriteLine();$0 : void", snippetTexts);
        }

        [Fact]
        public async Task Can_complete_parameter()
        {
            const string source = @"
                public class Class1
                {
                    public Class1()
                    {
                    }
                    public Class2(Class1 class1)
                    {
                        clas$$
                    }
                }
            ";

            var completions = await FindCompletionsAsync(source, wantSnippet: true);
            ContainsSnippet("class1$0 : Class1", completions);
        }

        [Fact]
        public async Task Can_return_keywords()
        {
            const string source = "usin$$";

            var completions = await FindCompletionsAsync(source, wantSnippet: true);
            ContainsSnippet("using", completions);
        }

        [Fact]
        public async Task Returns_enums()
        {
            const string source =
                @"public enum Colors { Red, Blue }

                  public class MyClass1
                  {
                      public MyClass1()
                      {
                          Col$$
                      }
                  }";

            var completions = await FindCompletionsAsync(source, wantSnippet: true);
            Assert.Equal(1, completions.Count());
            ContainsSnippet("Colors$0", completions);
        }

        [Fact]
        public async Task Returns_event_without_event_keyword()
        {
            const string source =
                @"
                public class MyClass1 {

                    public event TickHandler TickChanged;
                    public MyClass1()
                    {
                        Tick$$
                    }
                }";

            var completions = await FindCompletionsAsync(source, wantSnippet: true);
            Assert.Equal(1, completions.Count());
            ContainsSnippet("TickChanged$0 : TickHandler", completions);
        }

        [Fact]
        public async Task Returns_method_without_optional_params()
        {
            const string source = @"
                public class Class1
                {
                    public void OptionalParam(int i, string s = null)
                    {
                    }
                    public void DoSomething()
                    {
                        Opt$$
                    }
                }
            ";

            var completions = await FindCompletionsAsync(source, wantSnippet: true);
            ContainsSnippet("OptionalParam(${1:int i});$0 : void", completions);
            ContainsSnippet("OptionalParam(${1:int i}, ${2:string s = null});$0 : void", completions);
        }

        private static IEnumerable<string> GetSnippetTexts(IEnumerable<AutoCompleteResponse> responses)
        {
            return responses.Select(r =>
                r.ReturnType != null
                    ? r.Snippet + " : " + r.ReturnType
                    : r.Snippet);
        }

        private void ContainsSnippet(string expected, IEnumerable<AutoCompleteResponse> responses)
        {
            var snippetTexts = GetSnippetTexts(responses);

            if (!snippetTexts.Contains(expected))
            {
                var builder = new StringBuilder();
                builder.AppendLine("Did not find - " + expected);

                foreach (var snippetText in snippetTexts)
                {
                    builder.AppendLine(snippetText);
                }

                this._logger.LogError(builder.ToString());
            }

            Assert.Contains(expected, snippetTexts);
        }
    }
}
