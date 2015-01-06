using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using Microsoft.AspNet.Mvc;
using Xunit;
using OmniSharp.Models;

namespace OmniSharp.Tests
{
    public class SnippetFacts
    {
        [Fact]
        public async Task Can_template_generic_type_argument()
        {
            var source =
                @"public class Class1 {
                    public Class1()
                        {
                            var l = new System.Collections.Generic.Lis$
                        }
                    }";

            var completions = await FindCompletionsAsync(source);
            ContainsSnippet("List<${1:T}>()$0", completions);
        }

        [Fact]
        public async Task Can_return_method_type_arguments_snippets()
        {
            var source =
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
                             someObj.G$
                         }
                     }";
            
            var completions = await FindCompletionsAsync(source);
            ContainsSnippet("Get<${1:SomeType}>()$0", completions);
        }

        [Fact]
        public async Task Does_not_include_tsource_argument_type()
        {
            var source =
                @"using System.Collections.Generic;
                using System.Linq;
                public class Class1 {
                    public Class1()
                    {
                        var l = new List<string>();
                        l.Firs$
                    }
                }";

            var completions = await FindCompletionsAsync(source);
            ContainsSnippet("First()$0", completions);
            ContainsSnippet("FirstOrDefault(${1:Func<string, bool> predicate})$0", completions);
        }

        [Fact]
        public async Task Does_not_include_tresult_argument_type()
        {
            var source =
                @"using System.Collections.Generic;
                     using System.Linq;
                     public class Class1 {
                         public Class1()
                         {
                             var dict = new Dictionary<string, object>();
                             dict.Sel$
                         }
                     }";
            
            var completions = await FindCompletionsAsync(source);
            ContainsSnippet("Select(${1:Func<KeyValuePair<string, object>, TResult> selector})$0", completions);
        }

        [Fact]
        public async Task Can_template_field()
        {
            var source =
                @"using System.Collections.Generic;

                 public class Class1 {
                     public int someField;
                     public Class1()
                     {
                         somef$
                     }
                 }";
            
            var completions = await FindCompletionsAsync(source);
            ContainsSnippet("someField$0", completions);
        }

        [Fact]
        public async Task Can_return_all_constructors()
        {
            var source =
                @"public class MyClass {
                    public MyClass() {}
                    public MyClass(int param) {}
                    public MyClass(int param, string param) {}
                }

                public class Class2 {
                    public Class2()
                    {
                        var c = new My$
                    }
                }";

            var completions = await FindCompletionsAsync(source);
            ContainsSnippet("MyClass()$0", completions);
            ContainsSnippet("MyClass(${1:int param})$0", completions);
            ContainsSnippet("MyClass(${1:int param}, ${2:string param})$0", completions);
        }

        
        [Fact]
        public async Task Can_template_generic_type_arguments()
        {
            var source =
                @"using System.Collections.Generic;
                  public class Class1 {
                      public Class1()
                      {
                          var l = new Dict$
                      }
                  }";
            
            var completions = await FindCompletionsAsync(source);
            ContainsSnippet("Dictionary<${1:TKey}, ${2:TValue}>()$0", completions);
        }

        [Fact]
        public async Task Can_template_parameter()
        {
            var source =
                @"using System.Collections.Generic;
                  public class Class1 {
                      public Class1()
                      {
                          var l = new Lis$
                      }
                  }";
            
            var completions = await FindCompletionsAsync(source);
            ContainsSnippet("List<${1:T}>(${2:IEnumerable<T> collection})$0", completions);

        }

        [Fact]
        public async Task Can_complete_namespace()
        {
            var source = @"using Sys$";

            var completions = await FindCompletionsAsync(source);
            ContainsSnippet("System$0", completions);
        }

        private void ContainsSnippet(string expected, IEnumerable<string> completions)
        {
            if (!completions.Contains(expected))
            {
                System.Console.Error.WriteLine("Did not find - " + expected);
                foreach (var completion in completions)
                {
                    System.Console.WriteLine(completion);
                }
            }
            Assert.Contains(expected, completions);
        }

        private async Task<IEnumerable<string>> FindCompletionsAsync(string source)
        {
            var workspace = TestHelpers.CreateSimpleWorkspace(source);
            var controller = new OmnisharpController(workspace);
            var request = CreateRequest(source);
            var response = await controller.AutoComplete(request);
            var completions = ((ObjectResult)response).Value as IEnumerable<AutoCompleteResponse>;
            return completions.Select(completion => completion.Snippet);
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
                WantSnippet = true
            };
        }

        private static string GetPartialWord(string editorText)
        {
            MatchCollection matches = Regex.Matches(editorText, @"([a-zA-Z0-9_]*)\$");
            return matches[0].Groups[1].ToString();
        }
    }
}