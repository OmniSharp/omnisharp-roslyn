using System.IO;
using System.Threading.Tasks;
using System.Linq;
using OmniSharp.Models.TypeLookup;
using OmniSharp.Options;
using OmniSharp.Roslyn.CSharp.Services.Types;
using TestUtility;
using Xunit;
using Xunit.Abstractions;

namespace OmniSharp.Roslyn.CSharp.Tests
{
    public class TypeLookupFacts : AbstractSingleRequestHandlerTestFixture<TypeLookupService>
    {
        public TypeLookupFacts(ITestOutputHelper output)
            : base(output)
        {
        }

        protected override string EndpointName => OmniSharpEndpoints.TypeLookup;

        [Fact]
        public async Task OmitsNamespaceForNonRegularCSharpSyntax()
        {
            var source = @"class Foo {}";

            var testFile = new TestFile("dummy.csx", source);
            var workspace = TestHelpers.CreateCsxWorkspace(testFile);

            var controller = new TypeLookupService(workspace, new FormattingOptions());
            var response = await controller.Handle(new TypeLookupRequest { FileName = testFile.FileName, Line = 0, Column = 7 });

            Assert.Equal("Foo", response.Type);
        }

        [Fact]
        public async Task TypesFromInlineAssemlbyReferenceContainDocumentation()
        {
            var testAssemblyPath = Path.Combine(TestAssets.Instance.TestBinariesFolder, "ClassLibraryWithDocumentation.dll");
            var source =
                $@"#r ""{testAssemblyPath}""
                using ClassLibraryWithDocumentation;
                Documented$$Class c;
            ";

            var testFile = new TestFile("dummy.csx", source);
            var position = testFile.Content.GetPointFromPosition();
            var workspace = TestHelpers.CreateCsxWorkspace(testFile);

            var controller = new TypeLookupService(workspace, new FormattingOptions());
            var response = await controller.Handle(new TypeLookupRequest { FileName = testFile.FileName, Line = position.Line, Column = position.Offset, IncludeDocumentation = true });

            Assert.Equal("This class performs an important function.", response.Documentation?.Trim());
        }

        [Fact]
        public async Task OmitsNamespaceForTypesInGlobalNamespace()
        {
            const string source = @"namespace Bar {
            class Foo {}
            }
            class Baz {}";

            var testFile = new TestFile("dummy.cs", source);
            using (var host = CreateOmniSharpHost(testFile))
            {
                var requestHandler = GetRequestHandler(host);

                var requestInNormalNamespace = new TypeLookupRequest { FileName = testFile.FileName, Line = 1, Column = 19 };
                var responseInNormalNamespace = await requestHandler.Handle(requestInNormalNamespace);

                var requestInGlobalNamespace = new TypeLookupRequest { FileName = testFile.FileName, Line = 3, Column = 19 };
                var responseInGlobalNamespace = await requestHandler.Handle(requestInGlobalNamespace);

                Assert.Equal("Bar.Foo", responseInNormalNamespace.Type);
                Assert.Equal("Baz", responseInGlobalNamespace.Type);
            }
        }

        [Fact]
        public async Task IncludesNamespaceForRegularCSharpSyntax()
        {
            const string source = @"namespace Bar {
            class Foo {}
            }";

            var testFile = new TestFile("dummy.cs", source);
            using (var host = CreateOmniSharpHost(testFile))
            {
                var requestHandler = GetRequestHandler(host);

                var request = new TypeLookupRequest { FileName = testFile.FileName, Line = 1, Column = 19 };
                var response = await requestHandler.Handle(request);

                Assert.Equal("Bar.Foo", response.Type);
            }
        }

        [Fact]
        public async Task IncludesContainingTypeFoNestedTypesForRegularCSharpSyntax()
        {
            var source = @"namespace Bar {
            class Foo {
                    class Xyz {}
                }   
            }";

            var testFile = new TestFile("dummy.cs", source);
            using (var host = CreateOmniSharpHost(testFile))
            {
                var requestHandler = GetRequestHandler(host);

                var request = new TypeLookupRequest { FileName = testFile.FileName, Line = 2, Column = 27 };
                var response = await requestHandler.Handle(request);

                Assert.Equal("Bar.Foo.Xyz", response.Type);
            }
        }

        [Fact]
        public async Task IncludesContainingTypeFoNestedTypesForNonRegularCSharpSyntax()
        {
            var source = @"class Foo {
                class Bar {}
            }";

            var testFile = new TestFile("dummy.csx", source);
            var workspace = TestHelpers.CreateCsxWorkspace(testFile);

            var controller = new TypeLookupService(workspace, new FormattingOptions());
            var request = new TypeLookupRequest { FileName = testFile.FileName, Line = 1, Column = 23 };
            var response = await controller.Handle(request);

            Assert.Equal("Foo.Bar", response.Type);
        }

        private static TestFile s_testFile = new TestFile("dummy.cs",
            @"using System;
            using Bar2;
            using System.Collections.Generic;
            namespace Bar {
                class Foo {
                    public Foo() {
                        Console.WriteLine(""abc"");
                    }

                    public void MyMethod(string name, Foo foo, Foo2 foo2) { };

                    private Foo2 _someField = new Foo2();

                    public Foo2 SomeProperty { get; }

                    public IDictionary<string, IEnumerable<int>> SomeDict { get; }

                    public void Compute(int index = 2) { }
                }
            }

            namespace Bar2 {
                class Foo2 {
                }
            }
            ");

        private async Task<TypeLookupResponse> GetTypeLookUpResponse(int line, int column)
        {
            using (var host = CreateOmniSharpHost(s_testFile))
            {
                var requestHandler = GetRequestHandler(host);
                var request = new TypeLookupRequest { FileName = s_testFile.FileName, Line = line, Column = column };

                return await requestHandler.Handle(request);
            }
        }

        [Fact]
        public async Task DisplayFormatForMethodSymbol_Invocation()
        {
            var response = await GetTypeLookUpResponse(line: 6, column: 35);

            Assert.Equal("void Console.WriteLine(string value)", response.Type);
        }

        [Fact]
        public async Task DisplayFormatForMethodSymbol_Declaration()
        {
            var response = await GetTypeLookUpResponse(line: 9, column: 35);
            Assert.Equal("void Foo.MyMethod(string name, Foo foo, Foo2 foo2)", response.Type);
        }

        [Fact]
        public async Task DisplayFormatFor_TypeSymbol_Primitive()
        {
            var response = await GetTypeLookUpResponse(line: 9, column: 46);
            Assert.Equal("System.String", response.Type);
        }

        [Fact]
        public async Task DisplayFormatFor_TypeSymbol_ComplexType_SameNamespace()
        {
            var response = await GetTypeLookUpResponse(line: 9, column: 56);
            Assert.Equal("Bar.Foo", response.Type);
        }

        [Fact]
        public async Task DisplayFormatFor_TypeSymbol_ComplexType_DifferentNamespace()
        {
            var response = await GetTypeLookUpResponse(line: 9, column: 67);
            Assert.Equal("Bar2.Foo2", response.Type);
        }

        [Fact]
        public async Task DisplayFormatFor_TypeSymbol_WithGenerics()
        {
            var response = await GetTypeLookUpResponse(line: 15, column: 36);
            Assert.Equal("System.Collections.Generic.IDictionary<System.String, System.Collections.Generic.IEnumerable<System.Int32>>", response.Type);
        }

        [Fact]
        public async Task DisplayFormatForParameterSymbol_Name_Primitive()
        {
            var response = await GetTypeLookUpResponse(line: 9, column: 51);
            Assert.Equal("string name", response.Type);
        }

        [Fact]
        public async Task DisplayFormatFor_ParameterSymbol_ComplexType_SameNamespace()
        {
            var response = await GetTypeLookUpResponse(line: 9, column: 60);
            Assert.Equal("Foo foo", response.Type);
        }

        [Fact]
        public async Task DisplayFormatFor_ParameterSymbol_Name_ComplexType_DifferentNamespace()
        {
            var response = await GetTypeLookUpResponse(line: 9, column: 71);
            Assert.Equal("Foo2 foo2", response.Type);
        }

        [Fact]
        public async Task DisplayFormatFor_ParameterSymbol_Name_WithDefaultValue()
        {
            var response = await GetTypeLookUpResponse(line: 17, column: 48);
            Assert.Equal("int index = 2", response.Type);
        }

        [Fact]
        public async Task DisplayFormatFor_FieldSymbol()
        {
            var response = await GetTypeLookUpResponse(line: 11, column: 38);
            Assert.Equal("Foo2 Foo._someField", response.Type);
        }

        [Fact]
        public async Task DisplayFormatFor_PropertySymbol()
        {
            var response = await GetTypeLookUpResponse(line: 13, column: 38);
            Assert.Equal("Foo2 Foo.SomeProperty", response.Type);
        }

        [Fact]
        public async Task DisplayFormatFor_PropertySymbol_WithGenerics()
        {
            var response = await GetTypeLookUpResponse(line: 15, column: 70);
            Assert.Equal("IDictionary<string, IEnumerable<int>> Foo.SomeDict", response.Type);
        }

        private async Task<TypeLookupResponse> GetTypeLookUpResponse(string content)
        {
            TestFile testFile = new TestFile("dummy.cs", content);
            using (var host = CreateOmniSharpHost(testFile))
            {
                var requestHandler = GetRequestHandler(host);
                var point = testFile.Content.GetPointFromPosition();
                var request = new TypeLookupRequest { FileName = testFile.FileName, Line = point.Line, Column = point.Offset };
                request.IncludeDocumentation = true;

                return await requestHandler.Handle(request);
            }
        }

        [Fact]
        public async Task StructuredDocumentationRemarksText()
        {
            string content = @"
class testissue
{
    ///<remarks>You may have some additional information about this class here.</remarks>
    public static bool C$$ompare(int gameObject, string tagName)
    {
        return gameObject.TagifyCompareTag(tagName);
    }
}";
            var response = await GetTypeLookUpResponse(content);
            var expected =
            @"You may have some additional information about this class here.";
            Assert.Equal(expected, response.StructuredDocumentation.RemarksText);
        }

        [Fact]
        public async Task StructuredDocumentationSummaryText()
        {
            string content = @"
class testissue
{
    ///<summary>Checks if object is tagged with the tag.</summary>
    public static bool C$$ompare(int gameObject, string tagName)
    {
    }
}";
            var response = await GetTypeLookUpResponse(content);
            var expected =
            @"Checks if object is tagged with the tag.";
            Assert.Equal(expected, response.StructuredDocumentation.SummaryText);
        }

        [Fact]
        public async Task StructuredDocumentationReturnsText()
        {
            string content = @"
class testissue
{
    ///<returns>Returns true if object is tagged with tag.</returns>
    public static bool C$$ompare(int gameObject, string tagName)
    {
    }
}";
            var response = await GetTypeLookUpResponse(content);
            var expected =
            @"Returns true if object is tagged with tag.";
            Assert.Equal(expected, response.StructuredDocumentation.ReturnsText);
        }

        [Fact]
        public async Task StructuredDocumentationExampleText()
        {
            string content = @"
class testissue
{
    ///<example>Checks if object is tagged with the tag.</example>
    public static bool C$$ompare(int gameObject, string tagName)
    {
    }
}";
            var response = await GetTypeLookUpResponse(content);
            var expected =
            @"Checks if object is tagged with the tag.";
            Assert.Equal(expected, response.StructuredDocumentation.ExampleText);
        }

        [Fact]
        public async Task StructuredDocumentationExceptionText()
        {
            string content = @"
class testissue
{
    ///<exception cref=""A"">A description</exception>
    ///<exception cref=""B"">B description</exception>
    public static bool C$$ompare(int gameObject, string tagName)
    {
    }
}";
            var response = await GetTypeLookUpResponse(content);
            Assert.Equal(2, response.StructuredDocumentation.Exception.Count());

            Assert.Equal("A", response.StructuredDocumentation.Exception[0].Name);
            Assert.Equal("A description", response.StructuredDocumentation.Exception[0].Documentation);
            Assert.Equal("B", response.StructuredDocumentation.Exception[1].Name);
            Assert.Equal("B description", response.StructuredDocumentation.Exception[1].Documentation);
        }

        [Fact]
        public async Task StructuredDocumentationParameter()
        {
            string content = @"
class testissue
{
    /// <param name=""gameObject"">The game object.</param> 
    /// <param name=""tagName"">Name of the tag.</param>
    public static bool C$$ompare(int gameObject, string tagName)
    {
    }
}";
            var response = await GetTypeLookUpResponse(content);
            Assert.Equal(2, response.StructuredDocumentation.ParamElements.Length);

            Assert.Equal("gameObject", response.StructuredDocumentation.ParamElements[0].Name);
            Assert.Equal("The game object.", response.StructuredDocumentation.ParamElements[0].Documentation);
            Assert.Equal("tagName", response.StructuredDocumentation.ParamElements[1].Name);
            Assert.Equal("Name of the tag.", response.StructuredDocumentation.ParamElements[1].Documentation);
        }

        [Fact]
        public async Task StructuredDocumentationTypeParameter()
        {
            string content = @"
public class TestClass
{
    /// <summary>
    /// Creates a new array of arbitrary type <typeparamref name=""T""/> and adds the elements of incoming list to it if possible
    /// </summary>
    /// <typeparam name=""T"">The element type of the array</typeparam>
    /// <typeparam name=""X"">The element type of the list</typeparam>
    public static T[] m$$kArray<T>(int n, List<X> list)
    {
        return new T[n];
    }
}";
            var response = await GetTypeLookUpResponse(content);
            Assert.Equal(2, response.StructuredDocumentation.TypeParamElements.Count());

            Assert.Equal("T", response.StructuredDocumentation.TypeParamElements[0].Name);
            Assert.Equal("The element type of the array", response.StructuredDocumentation.TypeParamElements[0].Documentation);
            Assert.Equal("X", response.StructuredDocumentation.TypeParamElements[1].Name);
            Assert.Equal("The element type of the list", response.StructuredDocumentation.TypeParamElements[1].Documentation);
        }

        [Fact]
        public async Task StructuredDocumentationValueText()
        {
            string content =
@"public class Employee
{
    private string _name;

    /// <summary>The Name property represents the employee's name.</summary>
    /// <value>The Name property gets/sets the value of the string field, _name.</value>
    public string Na$$me
    {
    }
}
";
            var response = await GetTypeLookUpResponse(content);
            var expectedValue =
            @"The Name property gets/sets the value of the string field, _name.";
            Assert.Equal(expectedValue, response.StructuredDocumentation.ValueText);
            var expectedSummary =
            @"The Name property represents the employee's name.";
            Assert.Equal(expectedSummary, response.StructuredDocumentation.SummaryText);
        }

        [Fact]
        public async Task StructuredDocumentationNestedTagSee()
        {
            string content = @"
public class TestClass
{
    /// <summary>DoWork is a method in the TestClass class. <see cref=""System.Console.WriteLine(System.String)""/> for information about output statements.</summary>
    public static void Do$$Work(int Int1)
    {
    }
}";
            var response = await GetTypeLookUpResponse(content);
            var expected =
            @"DoWork is a method in the TestClass class. System.Console.WriteLine(System.String) for information about output statements.";
            Assert.Equal(expected, response.StructuredDocumentation.SummaryText);
        }

        [Fact]
        public async Task StructuredDocumentationNestedTagParamRef()
        {
            string content = @"
public class TestClass
{
    /// <summary>Creates a new array of arbitrary type <typeparamref name=""T""/></summary>
    /// <typeparam name=""T"">The element type of the array</typeparam>
    public static T[] mk$$Array<T>(int n)
    {
        return new T[n];
    }
}";
            var response = await GetTypeLookUpResponse(content);
            var expected =
            @"Creates a new array of arbitrary type T ";
            Assert.Equal(expected, response.StructuredDocumentation.SummaryText);
        }

        [Fact]
        public async Task StructuredDocumentationNestedTagCode()
        {
            string content = @"
public class TestClass
{
    /// <example>This sample shows how to call the <see cref=""GetZero""/> method.
    /// <code>
    /// class TestClass 
    /// {
    ///     static int Main() 
    ///     {
    ///         return GetZero();
    ///     }
    /// }
    /// </code>
    /// </example>
    public static int $$GetZero()
    {
        return 0;
    }
}";
            var response = await GetTypeLookUpResponse(content);
            var expected =
            @"This sample shows how to call the TestClass.GetZero method.

    class TestClass 
    {
        static int Main() 
        {
            return GetZero();
        }
    }
    ";
            Assert.Equal(expected.Replace("\r", ""), response.StructuredDocumentation.ExampleText);
        }

        [Fact]
        public async Task StructuredDocumentationNestedTagPara()
        {
            string content = @"
public class TestClass
{
    /// <summary>DoWork is a method in the TestClass class.
    /// <para>Here's how you could make a second paragraph in a description.</para>
    /// </summary>
    public static void Do$$Work(int Int1)
    {
    }
}
            ";
            var response = await GetTypeLookUpResponse(content);
            var expected =
@"DoWork is a method in the TestClass class.

Here's how you could make a second paragraph in a description.";
            Assert.Equal(expected.Replace("\r", ""), response.StructuredDocumentation.SummaryText);
        }

        [Fact]
        public async Task StructuredDocumentationNestedTagSeeAlso()
        {
            string content = @"
public class TestClass
{
    /// <summary>DoWork is a method in the TestClass class.
    /// <seealso cref=""TestClass.Main""/>
    /// </summary>
            public static void Do$$Work(int Int1)
            {
            }

            static void Main()
            {
            }
}";
            var response = await GetTypeLookUpResponse(content);
            var expected =
@"DoWork is a method in the TestClass class.
See also: TestClass.Main ";
            Assert.Equal(expected.Replace("\r", ""), response.StructuredDocumentation.SummaryText);
        }

        [Fact]
        public async Task StructuredDocumentationSummaryAndParam()
        {
            string content = @"
class testissue
{
    ///<summary>Checks if object is tagged with the tag.</summary>
    /// <param name=""gameObject"">The game object.</param> 
    /// <param name=""tagName"">Name of the tag.</param>
    public static bool C$$ompare(int gameObject, string tagName)
    {
    }
}";
            var response = await GetTypeLookUpResponse(content);
            var expected =
            @"Checks if object is tagged with the tag.";
            Assert.Equal(expected, response.StructuredDocumentation.SummaryText);

            Assert.Equal(2, response.StructuredDocumentation.ParamElements.Length);
            Assert.Equal("gameObject", response.StructuredDocumentation.ParamElements[0].Name);
            Assert.Equal("The game object.", response.StructuredDocumentation.ParamElements[0].Documentation);
            Assert.Equal("tagName", response.StructuredDocumentation.ParamElements[1].Name);
            Assert.Equal("Name of the tag.", response.StructuredDocumentation.ParamElements[1].Documentation);
        }

        [Fact]
        public async Task StructuredDocumentationManyTags()
        {
            string content = @"
class testissue
{
    ///<summary>Checks if object is tagged with the tag.</summary>
    ///<param name=""gameObject"">The game object.</param> 
    ///<example>Invoke using A.Compare(5) where A is an instance of the class testissue.</example>
    ///<typeparam name=""T"">The element type of the array</typeparam>
    ///<exception cref=""System.Exception"">Thrown when something goes wrong</exception>
    ///<remarks>You may have some additional information about this class here.</remarks>
    ///<returns>Returns an array of type <typeparamref name=""T""/>.</returns>
    public static T[] C$$ompare(int gameObject)
    {
    }
}";
            var response = await GetTypeLookUpResponse(content);
            var expectedSummary =
            @"Checks if object is tagged with the tag.";
            Assert.Equal(expectedSummary, response.StructuredDocumentation.SummaryText);

            Assert.Single(response.StructuredDocumentation.ParamElements);
            Assert.Equal("gameObject", response.StructuredDocumentation.ParamElements[0].Name);
            Assert.Equal("The game object.", response.StructuredDocumentation.ParamElements[0].Documentation);
            
            var expectedExample =
            @"Invoke using A.Compare(5) where A is an instance of the class testissue.";
            Assert.Equal(expectedExample, response.StructuredDocumentation.ExampleText);

            Assert.Single(response.StructuredDocumentation.TypeParamElements);
            Assert.Equal("T", response.StructuredDocumentation.TypeParamElements[0].Name);
            Assert.Equal("The element type of the array", response.StructuredDocumentation.TypeParamElements[0].Documentation);

            Assert.Single(response.StructuredDocumentation.Exception);
            Assert.Equal("System.Exception", response.StructuredDocumentation.Exception[0].Name);
            Assert.Equal("Thrown when something goes wrong", response.StructuredDocumentation.Exception[0].Documentation);

            var expectedRemarks =
            @"You may have some additional information about this class here.";
            Assert.Equal(expectedRemarks, response.StructuredDocumentation.RemarksText);

            var expectedReturns =
            @"Returns an array of type T .";
            Assert.Equal(expectedReturns, response.StructuredDocumentation.ReturnsText);
        }
    }
}
