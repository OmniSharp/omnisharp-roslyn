// using System.Collections.Generic;
// using System.Linq;
// using System.Text.RegularExpressions;
// using System.Threading.Tasks;
// using OmniSharp.Models;
// using OmniSharp.Options;
// using OmniSharp.Roslyn.CSharp.Services.Intellisense;
// using OmniSharp.Tests;
// using Xunit;
//
// namespace OmniSharp.Roslyn.CSharp.Tests
// {
//     public class SnippetFacts
//     {
//         [Fact]
//         public async Task Can_template_generic_type_argument()
//         {
//             var source =
//                 @"public class Class1 {
//                     public Class1()
//                         {
//                             var l = new System.Collections.Generic.Lis$
//                         }
//                     }";
//
//             var completions = await FindCompletionsAsync(source);
//             ContainsSnippet("List<${1:T}>()$0", completions);
//         }
//
//         [Fact]
//         public async Task Can_return_method_type_arguments_snippets()
//         {
//             var source =
//                 @"using System.Collections.Generic;
//
//                      public class Test {
//                          public string Get<SomeType>()
//                          {
//                          }
//                      }
//                      public class Class1 {
//                          public Class1()
//                          {
//                              var someObj = new Test();
//                              someObj.G$
//                          }
//                      }";
//
//             var completions = await FindCompletionsAsync(source);
//             ContainsSnippet("Get<${1:SomeType}>()$0 : string", completions);
//         }
//
//         [Fact]
//         public async Task Does_not_include_tsource_argument_type()
//         {
//             var source =
//                 @"using System.Collections.Generic;
//                 using System.Linq;
//                 public class Class1 {
//                     public Class1()
//                     {
//                         var l = new List<string>();
//                         l.Firs$
//                     }
//                 }";
//
//             var completions = await FindCompletionsAsync(source);
//             ContainsSnippet("First()$0 : string", completions);
//             ContainsSnippet("FirstOrDefault(${1:Func<string, bool> predicate})$0 : string", completions);
//         }
//
//         [Fact]
//         public async Task Does_not_include_tresult_argument_type()
//         {
//             var source =
//                 @"using System.Collections.Generic;
//                      using System.Linq;
//                      public class Class1 {
//                          public Class1()
//                          {
//                              var dict = new Dictionary<string, object>();
//                              dict.Sel$
//                          }
//                      }";
//
//             var completions = await FindCompletionsAsync(source);
//             ContainsSnippet("Select(${1:Func<KeyValuePair<string, object>, TResult> selector})$0 : IEnumerable<TResult>", completions);
//         }
//
//         [Fact]
//         public async Task Can_template_field()
//         {
//             var source =
//                 @"using System.Collections.Generic;
//
//                  public class Class1 {
//                      public int someField;
//                      public Class1()
//                      {
//                          somef$
//                      }
//                  }";
//
//             var completions = await FindCompletionsAsync(source);
//             ContainsSnippet("someField$0 : int", completions);
//         }
//
//         [Fact]
//         public async Task Can_return_all_constructors()
//         {
//             var source =
//                 @"public class MyClass {
//                     public MyClass() {}
//                     public MyClass(int param) {}
//                     public MyClass(int param, string param) {}
//                 }
//
//                 public class Class2 {
//                     public Class2()
//                     {
//                         var c = new My$
//                     }
//                 }";
//
//             var completions = await FindCompletionsAsync(source);
//             ContainsSnippet("MyClass()$0", completions);
//             ContainsSnippet("MyClass(${1:int param})$0", completions);
//             ContainsSnippet("MyClass(${1:int param}, ${2:string param})$0", completions);
//         }
//
//
//         [Fact]
//         public async Task Can_template_generic_type_arguments()
//         {
//             var source =
//                 @"using System.Collections.Generic;
//                   public class Class1 {
//                       public Class1()
//                       {
//                           var l = new Dict$
//                       }
//                   }";
//
//             var completions = await FindCompletionsAsync(source);
//             ContainsSnippet("Dictionary<${1:TKey}, ${2:TValue}>()$0", completions);
//         }
//
//         [Fact]
//         public async Task Can_template_parameter()
//         {
//             var source =
//                 @"using System.Collections.Generic;
//                   public class Class1 {
//                       public Class1()
//                       {
//                           var l = new Lis$
//                       }
//                   }";
//
//             var completions = await FindCompletionsAsync(source);
//             ContainsSnippet("List<${1:T}>(${2:IEnumerable<T> collection})$0", completions);
//
//         }
//
//         [Fact]
//         public async Task Can_complete_namespace()
//         {
//             var source = @"using Sys$";
//
//             var completions = await FindCompletionsAsync(source);
//             ContainsSnippet("System$0", completions);
//         }
//
//         [Fact]
//         public async Task Can_complete_variable()
//         {
//             var source = @"
//                 public class Class1
//                 {
//                     public Class1()
//                     {
//                         var aVariable = 1;
//                         av$
//                     }
//                 }
//             ";
//
//             var completions = await FindCompletionsAsync(source);
//             ContainsSnippet("aVariable$0 : int", completions);
//         }
//
//         [Fact]
//         public async Task Void_methods_end_with_semicolons()
//         {
//             var source = @"
//                 using System;
//                 public class Class1
//                 {
//                     public Class1()
//                     {
//                         Console.WriteLi$
//                     }
//                 }
//             ";
//
//             var completions = await FindCompletionsAsync(source);
//             ContainsSnippet("WriteLine();$0 : void", completions);
//         }
//
//         [Fact]
//         public async Task Fuzzy_matches_are_returned_when_first_letters_match()
//         {
//             var source = @"
//                 using System;
//                 public class Class1
//                 {
//                     public Class1()
//                     {
//                         Console.wrl$
//                     }
//                 }
//             ";
//
//             var completions = await FindCompletionsAsync(source);
//             ContainsSnippet("WriteLine();$0 : void", completions);
//         }
//
//         [Fact]
//         public async Task Fuzzy_matches_are_not_returned_when_first_letters_do_not_match()
//         {
//             var source = @"
//                 using System;
//                 public class Class1
//                 {
//                     public Class1()
//                     {
//                         Console.rl$
//                     }
//                 }
//             ";
//
//             var completions = await FindCompletionsAsync(source);
//             Assert.DoesNotContain("WriteLine();$0 : void", completions);
//         }
//
//         [Fact]
//         public async Task Can_complete_parameter()
//         {
//             var source = @"
//                 public class Class1
//                 {
//                     public Class1()
//                     {
//                     }
//                     public Class2(Class1 class1)
//                     {
//                         clas$
//                     }
//                 }
//             ";
//
//             var completions = await FindCompletionsAsync(source);
//             ContainsSnippet("class1$0 : Class1", completions);
//         }
//
//         [Fact]
//         public async Task Can_return_keywords()
//         {
//             var source = @"usin$";
//
//             var completions = await FindCompletionsAsync(source);
//             ContainsSnippet("using", completions);
//         }
//
//         [Fact]
//         public async Task Returns_enums()
//         {
//             var source =
//                 @"public enum Colors { Red, Blue }
//
//                   public class MyClass1
//                   {
//                       public MyClass1()
//                       {
//                           Col$
//                       }
//                   }";
//
//             var completions = await FindCompletionsAsync(source);
//             Assert.Equal(1, completions.Count());
//             ContainsSnippet("Colors$0", completions);
//         }
//
//         [Fact]
//         public async Task Returns_event_without_event_keyword()
//         {
//             var source =
//                 @"
//                 public class MyClass1 {
//
//                     public event TickHandler TickChanged;
//                     public MyClass1()
//                     {
//                         Tick$
//                     }
//                 }";
//
//             var completions = await FindCompletionsAsync(source);
//             Assert.Equal(1, completions.Count());
//             ContainsSnippet("TickChanged$0", completions);
//         }
//
//         [Fact]
//         public async Task Returns_method_without_optional_params()
//         {
//             var source = @"
//                 public class Class1
//                 {
//                     public void OptionalParam(int i, string s = null)
//                     {
//                     }
//                     public void DoSomething()
//                     {
//                         Opt$
//                     }
//                 }
//             ";
//
//             var completions = await FindCompletionsAsync(source);
//             ContainsSnippet("OptionalParam(${1:int i});$0 : void", completions);
//             ContainsSnippet("OptionalParam(${1:int i}, ${2:string s = null});$0 : void", completions);
//         }
//
//         private void ContainsSnippet(string expected, IEnumerable<string> completions)
//         {
//             if (!completions.Contains(expected))
//             {
//                 System.Console.Error.WriteLine("Did not find - " + expected);
//                 foreach (var completion in completions)
//                 {
//                     System.Console.WriteLine(completion);
//                 }
//             }
//             Assert.Contains(expected, completions);
//         }
//
//         private async Task<IEnumerable<string>> FindCompletionsAsync(string source)
//         {
//             var workspace = await TestHelpers.CreateSimpleWorkspace(source);
//             var controller = new IntellisenseService(workspace, new FormattingOptions());
//             var request = CreateRequest(source);
//             var response = await controller.Handle(request);
//             var completions = response as IEnumerable<AutoCompleteResponse>;
//             return completions.Select(completion => BuildCompletion(completion));
//         }
//
//         private string BuildCompletion(AutoCompleteResponse completion)
//         {
//             string result = completion.Snippet;
//             if (completion.ReturnType != null)
//             {
//                 result += " : " + completion.ReturnType;
//             }
//             return result;
//         }
//
//         private AutoCompleteRequest CreateRequest(string source, string fileName = "dummy.cs")
//         {
//             var lineColumn = TestHelpers.GetLineAndColumnFromDollar(source);
//             return new AutoCompleteRequest
//             {
//                 Line = lineColumn.Line,
//                 Column = lineColumn.Column,
//                 FileName = fileName,
//                 Buffer = source.Replace("$", ""),
//                 WordToComplete = GetPartialWord(source),
//                 WantSnippet = true,
//                 WantReturnType = true
//             };
//         }
//
//         private static string GetPartialWord(string editorText)
//         {
//             MatchCollection matches = Regex.Matches(editorText, @"([a-zA-Z0-9_]*)\$");
//             return matches[0].Groups[1].ToString();
//         }
//     }
// }
