using System.IO;
using System.Threading.Tasks;
using OmniSharp.Models.v2;
using OmniSharp.Options;
using OmniSharp.Roslyn.CSharp.Services;
using TestUtility;
using Xunit;
using Xunit.Abstractions;

namespace OmniSharp.Roslyn.CSharp.Tests
{
    public class QuickInfoProviderFacts : AbstractSingleRequestHandlerTestFixture<QuickInfoProvider>
    {
        protected override string EndpointName => OmniSharpEndpoints.V2.QuickInfo;

        public QuickInfoProviderFacts(ITestOutputHelper output, SharedOmniSharpHostFixture sharedOmniSharpHostFixture)
            : base(output, sharedOmniSharpHostFixture)
        { }

        [Fact]
        public async Task ParameterDocumentation()
        {
            const string source = @"namespace N
{
    class C
    {
        /// <param name=""i"">Some content <see cref=""C""/></param>
        public void M(int i)
        {
            _ = i;
        }
    }
}";

            var testFile = new TestFile("dummy.cs", source);
            SharedOmniSharpTestHost.AddFilesToWorkspace(testFile);
            var requestHandler = GetRequestHandler(SharedOmniSharpTestHost);

            var request = new QuickInfoRequest { FileName = testFile.FileName, Line = 7, Column = 17 };
            var response = await requestHandler.Handle(request);

            Assert.Equal("(parameter) int i", response.Description);
            Assert.Equal("Some content `C`", response.Summary);
            Assert.Empty(response.RemainingSections);
        }

        [Fact]
        public async Task OmitsNamespaceForNonRegularCSharpSyntax()
        {
            var source = @"class Foo {}";

            var testFile = new TestFile("dummy.csx", source);
            var workspace = TestHelpers.CreateCsxWorkspace(testFile);

            var controller = new QuickInfoProvider(workspace, new FormattingOptions());
            var response = await controller.Handle(new QuickInfoRequest { FileName = testFile.FileName, Line = 0, Column = 7 });

            Assert.Equal("class Foo", response.Description);
            Assert.Null(response.Summary);
            Assert.Empty(response.RemainingSections);
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

            var controller = new QuickInfoProvider(workspace, new FormattingOptions());
            var response = await controller.Handle(new QuickInfoRequest { FileName = testFile.FileName, Line = position.Line, Column = position.Offset });

            Assert.Equal("class ClassLibraryWithDocumentation.DocumentedClass", response.Description);
            Assert.Equal("This class performs an important function.", response.Summary?.Trim());
            Assert.Empty(response.RemainingSections);
        }

        [Fact]
        public async Task OmitsNamespaceForTypesInGlobalNamespace()
        {
            const string source = @"namespace Bar {
            class Foo {}
            }
            class Baz {}";

            var testFile = new TestFile("dummy.cs", source);
            SharedOmniSharpTestHost.AddFilesToWorkspace(testFile);
            var requestHandler = GetRequestHandler(SharedOmniSharpTestHost);

            var requestInNormalNamespace = new QuickInfoRequest { FileName = testFile.FileName, Line = 1, Column = 19 };
            var responseInNormalNamespace = await requestHandler.Handle(requestInNormalNamespace);

            var requestInGlobalNamespace = new QuickInfoRequest { FileName = testFile.FileName, Line = 3, Column = 19 };
            var responseInGlobalNamespace = await requestHandler.Handle(requestInGlobalNamespace);

            Assert.Equal("class Bar.Foo", responseInNormalNamespace.Description);
            Assert.Null(responseInNormalNamespace.Summary);
            Assert.Empty(responseInNormalNamespace.RemainingSections);
            Assert.Equal("class Baz", responseInGlobalNamespace.Description);
            Assert.Null(responseInGlobalNamespace.Summary);
            Assert.Empty(responseInGlobalNamespace.RemainingSections);
        }

        [Fact]
        public async Task IncludesNamespaceForRegularCSharpSyntax()
        {
            const string source = @"namespace Bar {
            class Foo {}
            }";

            var testFile = new TestFile("dummy.cs", source);
            SharedOmniSharpTestHost.AddFilesToWorkspace(testFile);
            var requestHandler = GetRequestHandler(SharedOmniSharpTestHost);

            var request = new QuickInfoRequest { FileName = testFile.FileName, Line = 1, Column = 19 };
            var response = await requestHandler.Handle(request);

            Assert.Equal("class Bar.Foo", response.Description);
            Assert.Null(response.Summary);
            Assert.Empty(response.RemainingSections);
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
            SharedOmniSharpTestHost.AddFilesToWorkspace(testFile);
            var requestHandler = GetRequestHandler(SharedOmniSharpTestHost);

            var request = new QuickInfoRequest { FileName = testFile.FileName, Line = 2, Column = 27 };
            var response = await requestHandler.Handle(request);

            Assert.Equal("class Bar.Foo.Xyz", response.Description);
            Assert.Null(response.Summary);
            Assert.Empty(response.RemainingSections);
        }

        [Fact]
        public async Task IncludesContainingTypeFoNestedTypesForNonRegularCSharpSyntax()
        {
            var source = @"class Foo {
                class Bar {}
            }";

            var testFile = new TestFile("dummy.csx", source);
            var workspace = TestHelpers.CreateCsxWorkspace(testFile);

            var controller = new QuickInfoProvider(workspace, new FormattingOptions());
            var request = new QuickInfoRequest { FileName = testFile.FileName, Line = 1, Column = 23 };
            var response = await controller.Handle(request);

            Assert.Equal("class Foo.Bar", response.Description);
            Assert.Null(response.Summary);
            Assert.Empty(response.RemainingSections);
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

                    private const int foo = 1;
                }
            }

            namespace Bar2 {
                class Foo2 {
                }
            }

            namespace Bar3 {
                enum Foo3 {
                    Val1 = 1,
                    Val2
                }
            }
            ");

        [Fact]
        public async Task DisplayFormatForMethodSymbol_Invocation()
        {
            var response = await GetTypeLookUpResponse(line: 6, column: 35);

            Assert.Equal("void Console.WriteLine(string value) (+ 18 overloads)", response.Description);
            Assert.Null(response.Summary);
            Assert.Empty(response.RemainingSections);
        }

        [Fact]
        public async Task DisplayFormatForMethodSymbol_Declaration()
        {
            var response = await GetTypeLookUpResponse(line: 9, column: 35);
            Assert.Equal("void Foo.MyMethod(string name, Foo foo, Foo2 foo2)", response.Description);
            Assert.Null(response.Summary);
            Assert.Empty(response.RemainingSections);
        }

        [Fact]
        public async Task DisplayFormatFor_TypeSymbol_Primitive()
        {
            var response = await GetTypeLookUpResponse(line: 9, column: 46);
            Assert.Equal("class System.String", response.Description);
            Assert.Null(response.Summary);
            Assert.Empty(response.RemainingSections);
        }

        [Fact]
        public async Task DisplayFormatFor_TypeSymbol_ComplexType_SameNamespace()
        {
            var response = await GetTypeLookUpResponse(line: 9, column: 56);
            Assert.Equal("class Bar.Foo", response.Description);
            Assert.Null(response.Summary);
            Assert.Empty(response.RemainingSections);
        }

        [Fact]
        public async Task DisplayFormatFor_TypeSymbol_ComplexType_DifferentNamespace()
        {
            var response = await GetTypeLookUpResponse(line: 9, column: 67);
            Assert.Equal("class Bar2.Foo2", response.Description);
            Assert.Null(response.Summary);
            Assert.Empty(response.RemainingSections);
        }

        [Fact]
        public async Task DisplayFormatFor_TypeSymbol_WithGenerics()
        {
            var response = await GetTypeLookUpResponse(line: 15, column: 36);
            Assert.Equal("interface System.Collections.Generic.IDictionary<TKey, TValue>", response.Description);
            Assert.Null(response.Summary);
            Assert.Equal(new[]
            {
                new QuickInfoResponseSection{ IsCSharpCode = true, Text = @"
TKey is string
TValue is IEnumerable<int>" }
            }, response.RemainingSections);
        }

        [Fact]
        public async Task DisplayFormatForParameterSymbol_Name_Primitive()
        {
            var response = await GetTypeLookUpResponse(line: 9, column: 51);
            Assert.Equal("(parameter) string name", response.Description);
            Assert.Null(response.Summary);
            Assert.Empty(response.RemainingSections);
        }

        [Fact]
        public async Task DisplayFormatFor_ParameterSymbol_ComplexType_SameNamespace()
        {
            var response = await GetTypeLookUpResponse(line: 9, column: 60);
            Assert.Equal("(parameter) Foo foo", response.Description);
            Assert.Null(response.Summary);
            Assert.Empty(response.RemainingSections);
        }

        [Fact]
        public async Task DisplayFormatFor_ParameterSymbol_Name_ComplexType_DifferentNamespace()
        {
            var response = await GetTypeLookUpResponse(line: 9, column: 71);
            Assert.Equal("(parameter) Foo2 foo2", response.Description);
            Assert.Null(response.Summary);
            Assert.Empty(response.RemainingSections);
        }

        [Fact]
        public async Task DisplayFormatFor_ParameterSymbol_Name_WithDefaultValue()
        {
            var response = await GetTypeLookUpResponse(line: 17, column: 48);
            Assert.Equal("(parameter) int index = 2", response.Description);
            Assert.Null(response.Summary);
            Assert.Empty(response.RemainingSections);
        }

        [Fact]
        public async Task DisplayFormatFor_FieldSymbol()
        {
            var response = await GetTypeLookUpResponse(line: 11, column: 38);
            Assert.Equal("(field) Foo2 Foo._someField", response.Description);
            Assert.Null(response.Summary);
            Assert.Empty(response.RemainingSections);
        }

        [Fact]
        public async Task DisplayFormatFor_FieldSymbol_WithConstantValue()
        {
            var response = await GetTypeLookUpResponse(line: 19, column: 41);
            Assert.Equal("(constant) int Foo.foo = 1", response.Description);
            Assert.Null(response.Summary);
            Assert.Empty(response.RemainingSections);
        }

        [Fact]
        public async Task DisplayFormatFor_EnumValue()
        {
            var response = await GetTypeLookUpResponse(line: 31, column: 23);
            Assert.Equal("Foo3.Val2 = 2", response.Description);
            Assert.Null(response.Summary);
            Assert.Empty(response.RemainingSections);
        }

        [Fact]
        public async Task DisplayFormatFor_PropertySymbol()
        {
            var response = await GetTypeLookUpResponse(line: 13, column: 38);
            Assert.Equal("Foo2 Foo.SomeProperty { get; }", response.Description);
            Assert.Null(response.Summary);
            Assert.Empty(response.RemainingSections);
        }

        [Fact]
        public async Task DisplayFormatFor_PropertySymbol_WithGenerics()
        {
            var response = await GetTypeLookUpResponse(line: 15, column: 70);
            Assert.Equal("IDictionary<string, IEnumerable<int>> Foo.SomeDict { get; }", response.Description);
            Assert.Null(response.Summary);
            Assert.Empty(response.RemainingSections);
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
            Assert.Equal("bool testissue.Compare(int gameObject, string tagName)", response.Description);
            Assert.Null(response.Summary);
            Assert.Equal(
                new[] { new QuickInfoResponseSection { IsCSharpCode = false, Text = "You may have some additional information about this class here." } },
                response.RemainingSections);
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
            Assert.Equal("bool testissue.Compare(int gameObject, string tagName)", response.Description);
            Assert.Equal("Checks if object is tagged with the tag.", response.Summary);
            Assert.Empty(response.RemainingSections);
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
            Assert.Equal("bool testissue.Compare(int gameObject, string tagName)", response.Description);
            Assert.Null(response.Summary);
            Assert.Equal(new[] { new QuickInfoResponseSection { IsCSharpCode = false, Text = "Returns:\n\n  Returns true if object is tagged with tag." } },
                response.RemainingSections);
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
            //var expected =
            //@"Checks if object is tagged with the tag.";
            Assert.Equal("bool testissue.Compare(int gameObject, string tagName)", response.Description);
            Assert.Null(response.Summary);
            Assert.Empty(response.RemainingSections);
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
            Assert.Equal("bool testissue.Compare(int gameObject, string tagName)", response.Description);
            Assert.Null(response.Summary);
            Assert.Equal(new[] { new QuickInfoResponseSection { IsCSharpCode = false, Text = "Exceptions:\n\n  A\n\n  B" } },
                response.RemainingSections);
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
            Assert.Equal("bool testissue.Compare(int gameObject, string tagName)", response.Description);
            Assert.Null(response.Summary);
            Assert.Empty(response.RemainingSections);
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
            Assert.Equal("T[] TestClass.mkArray<T>(int n, List<X> list)", response.Description);
            Assert.Equal("Creates a new array of arbitrary type `T` and adds the elements of incoming list to it if possible", response.Summary);
            Assert.Empty(response.RemainingSections);
        }

        [Fact]
        public async Task StructuredDocumentationTypeParameter_TypeParam1()
        {
            string content = @"
public class TestClass
{
    /// <summary>
    /// Creates a new array of arbitrary type <typeparamref name=""T""/> and adds the elements of incoming list to it if possible
    /// </summary>
    /// <typeparam name=""T"">The element type of the array</typeparam>
    /// <typeparam name=""X"">The element type of the list</typeparam>
    public static T[] mkArray<T$$>(int n, List<X> list)
    {
        return new T[n];
    }
}";
            var response = await GetTypeLookUpResponse(content);
            Assert.Equal("T in TestClass.mkArray<T>", response.Description);
            Assert.Equal("The element type of the array", response.Summary);
            Assert.Empty(response.RemainingSections);
        }

        [Fact]
        public async Task StructuredDocumentationTypeParameter_TypeParam2()
        {
            string content = @"
public class TestClass
{
    /// <summary>
    /// Creates a new array of arbitrary type <typeparamref name=""T""/> and adds the elements of incoming list to it if possible
    /// </summary>
    /// <typeparam name=""T"">The element type of the array</typeparam>
    /// <typeparam name=""X"">The element type of the list</typeparam>
    public static T[] mkArray<T>(int n, List<X$$> list)
    {
        return new T[n];
    }
}";
            var response = await GetTypeLookUpResponse(content);
            Assert.Null(response.Description);
            Assert.Null(response.Summary);
            Assert.Null(response.RemainingSections);
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
            Assert.Equal("string Employee.Name { }", response.Description);
            Assert.Equal("The Name property represents the employee's name.", response.Summary);
            Assert.Equal(new[] { new QuickInfoResponseSection { IsCSharpCode = false, Text = "Value:\n\n  The Name property gets/sets the value of the string field, _name." } },
                response.RemainingSections);
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
            Assert.Equal("void TestClass.DoWork(int Int1)", response.Description);
            Assert.Equal("DoWork is a method in the TestClass class. `System.Console.WriteLine(string)` for information about output statements.",
                         response.Summary);
            Assert.Empty(response.RemainingSections);
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
            Assert.Equal("T[] TestClass.mkArray<T>(int n)", response.Description);
            Assert.Equal("Creates a new array of arbitrary type `T`", response.Summary);
            Assert.Empty(response.RemainingSections);
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
            Assert.Equal("int TestClass.GetZero()", response.Description);
            Assert.Null(response.Summary);
            Assert.Empty(response.RemainingSections);
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
            Assert.Equal("void TestClass.DoWork(int Int1)", response.Description);
            Assert.Equal("DoWork is a method in the TestClass class.\n\n\n\nHere's how you could make a second paragraph in a description.", response.Summary);
            Assert.Empty(response.RemainingSections);
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
            Assert.Equal("void TestClass.DoWork(int Int1)", response.Description);
            Assert.Equal("DoWork is a method in the TestClass class. `TestClass.Main()`", response.Summary);
            Assert.Empty(response.RemainingSections);
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
            Assert.Equal("bool testissue.Compare(int gameObject, string tagName)", response.Description);
            Assert.Equal("Checks if object is tagged with the tag.", response.Summary);
            Assert.Empty(response.RemainingSections);
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
            Assert.Equal("T[] testissue.Compare(int gameObject)", response.Description);
            Assert.Equal("Checks if object is tagged with the tag.", response.Summary);
            Assert.Equal(new[] {
                new QuickInfoResponseSection { IsCSharpCode = false, Text = "You may have some additional information about this class here." },
                new QuickInfoResponseSection { IsCSharpCode = false, Text = "Returns:\n\n  Returns an array of type `T`." },
                new QuickInfoResponseSection { IsCSharpCode = false, Text = "Exceptions:\n\n  `System.Exception`" }
            }, response.RemainingSections);
        }

        [Fact]
        public async Task StructuredDocumentationSpaceBeforeText()
        {
            string content = @"
public class TestClass
{
    /// <summary><c>DoWork</c> is a method in the <c>TestClass</c> class.</summary>
    public static void Do$$Work(int Int1)
    {
    }
}";
            var response = await GetTypeLookUpResponse(content);
            Assert.Equal("void TestClass.DoWork(int Int1)", response.Description);
            Assert.Equal("DoWork is a method in the TestClass class.", response.Summary);
            Assert.Empty(response.RemainingSections);
        }

        [Fact]
        public async Task StructuredDocumentationForParameters1()
        {
            string content = @"
class testissue
{
    /// <param name=""gameObject"">The game object.</param> 
    /// <param name=""tagName"">Name of the tag.</param>
    public static bool Compare(int gam$$eObject, string tagName)
    {
    }
}";
            var response = await GetTypeLookUpResponse(content);
            Assert.Equal("(parameter) int gameObject", response.Description);
            Assert.Equal("The game object.", response.Summary);
            Assert.Empty(response.RemainingSections);
        }

        [Fact]
        public async Task StructuredDocumentationForParameters2()
        {
            string content = @"
class testissue
{
    /// <param name=""gameObject"">The game object.</param> 
    /// <param name=""tagName"">Name of the tag.</param>
    public static bool Compare(int gameObject, string tag$$Name)
    {
    }
}";
            var response = await GetTypeLookUpResponse(content);
            Assert.Equal("(parameter) string tagName", response.Description);
            Assert.Equal("Name of the tag.", response.Summary);
            Assert.Empty(response.RemainingSections);
        }

        private async Task<QuickInfoResponse> GetTypeLookUpResponse(string content)
        {
            TestFile testFile = new TestFile("dummy.cs", content);
            SharedOmniSharpTestHost.AddFilesToWorkspace(testFile);
            var requestHandler = GetRequestHandler(SharedOmniSharpTestHost);
            var point = testFile.Content.GetPointFromPosition();
            var request = new QuickInfoRequest { FileName = testFile.FileName, Line = point.Line, Column = point.Offset };

            return await requestHandler.Handle(request);
        }

        private async Task<QuickInfoResponse> GetTypeLookUpResponse(int line, int column)
        {
            SharedOmniSharpTestHost.AddFilesToWorkspace(s_testFile);
            var requestHandler = GetRequestHandler(SharedOmniSharpTestHost);
            var request = new QuickInfoRequest { FileName = s_testFile.FileName, Line = line, Column = column };

            return await requestHandler.Handle(request);
        }
    }
}
