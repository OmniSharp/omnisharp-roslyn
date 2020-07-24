using System.Collections.Generic;
using System.Collections.Immutable;
using System.IO;
using System.Threading.Tasks;
using OmniSharp.Models;
using OmniSharp.Options;
using OmniSharp.Roslyn.CSharp.Services;
using TestUtility;
using Xunit;
using Xunit.Abstractions;

#nullable enable

namespace OmniSharp.Roslyn.CSharp.Tests
{
    public class QuickInfoProviderFacts : AbstractSingleRequestHandlerTestFixture<QuickInfoProvider>
    {
        protected override string EndpointName => OmniSharpEndpoints.QuickInfo;

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

            AssertContents(response.Sections, "(parameter) int i", "Some content `C`");
        }

        [Fact]
        public async Task OmitsNamespaceForNonRegularCSharpSyntax()
        {
            var source = @"class Foo {}";

            var testFile = new TestFile("dummy.csx", source);
            var workspace = TestHelpers.CreateCsxWorkspace(testFile);

            var controller = new QuickInfoProvider(workspace, new FormattingOptions(), null);
            var response = await controller.Handle(new QuickInfoRequest { FileName = testFile.FileName, Line = 0, Column = 7 });

            AssertContents(response.Sections, "class Foo");
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

            var controller = new QuickInfoProvider(workspace, new FormattingOptions(), null);
            var response = await controller.Handle(new QuickInfoRequest { FileName = testFile.FileName, Line = position.Line, Column = position.Offset });

            AssertContents(response.Sections, "class ClassLibraryWithDocumentation.DocumentedClass", "This class performs an important function.");
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

            AssertContents(responseInNormalNamespace.Sections, "class Bar.Foo");
            AssertContents(responseInGlobalNamespace.Sections, "class Baz");
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

            AssertContents(response.Sections, "class Bar.Foo");
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

            AssertContents(response.Sections, "class Bar.Foo.Xyz");
        }

        [Fact]
        public async Task IncludesContainingTypeFoNestedTypesForNonRegularCSharpSyntax()
        {
            var source = @"class Foo {
                class Bar {}
            }";

            var testFile = new TestFile("dummy.csx", source);
            var workspace = TestHelpers.CreateCsxWorkspace(testFile);

            var controller = new QuickInfoProvider(workspace, new FormattingOptions(), null);
            var request = new QuickInfoRequest { FileName = testFile.FileName, Line = 1, Column = 23 };
            var response = await controller.Handle(request);

            AssertContents(response.Sections, "class Foo.Bar");
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

            AssertContents(response.Sections, "void Console.WriteLine(string value) (+ 18 overloads)");
        }

        [Fact]
        public async Task DisplayFormatForMethodSymbol_Declaration()
        {
            var response = await GetTypeLookUpResponse(line: 9, column: 35);
            AssertContents(response.Sections, "void Foo.MyMethod(string name, Foo foo, Foo2 foo2)");
        }

        [Fact]
        public async Task DisplayFormatFor_TypeSymbol_Primitive()
        {
            var response = await GetTypeLookUpResponse(line: 9, column: 46);
            AssertContents(response.Sections, "class System.String");
        }

        [Fact]
        public async Task DisplayFormatFor_TypeSymbol_ComplexType_SameNamespace()
        {
            var response = await GetTypeLookUpResponse(line: 9, column: 56);
            AssertContents(response.Sections, "class Bar.Foo");
        }

        [Fact]
        public async Task DisplayFormatFor_TypeSymbol_ComplexType_DifferentNamespace()
        {
            var response = await GetTypeLookUpResponse(line: 9, column: 67);
            AssertContents(response.Sections, "class Bar2.Foo2");
        }

        [Fact]
        public async Task DisplayFormatFor_TypeSymbol_WithGenerics()
        {
            var response = await GetTypeLookUpResponse(line: 15, column: 36);
            AssertContents(response.Sections, "interface System.Collections.Generic.IDictionary<TKey, TValue>",
                new QuickInfoResponseSection { IsCSharpCode = true, Text = $"TKey is string" },
                new QuickInfoResponseSection { IsCSharpCode = true, Text = $"TValue is IEnumerable<int>" });
        }

        [Fact]
        public async Task DisplayFormatForParameterSymbol_Name_Primitive()
        {
            var response = await GetTypeLookUpResponse(line: 9, column: 51);
            AssertContents(response.Sections, "(parameter) string name");
        }

        [Fact]
        public async Task DisplayFormatFor_ParameterSymbol_ComplexType_SameNamespace()
        {
            var response = await GetTypeLookUpResponse(line: 9, column: 60);
            AssertContents(response.Sections, "(parameter) Foo foo");
        }

        [Fact]
        public async Task DisplayFormatFor_ParameterSymbol_Name_ComplexType_DifferentNamespace()
        {
            var response = await GetTypeLookUpResponse(line: 9, column: 71);
            AssertContents(response.Sections, "(parameter) Foo2 foo2");
        }

        [Fact]
        public async Task DisplayFormatFor_ParameterSymbol_Name_WithDefaultValue()
        {
            var response = await GetTypeLookUpResponse(line: 17, column: 48);
            AssertContents(response.Sections, "(parameter) int index = 2");
        }

        [Fact]
        public async Task DisplayFormatFor_FieldSymbol()
        {
            var response = await GetTypeLookUpResponse(line: 11, column: 38);
            AssertContents(response.Sections, "(field) Foo2 Foo._someField");
        }

        [Fact]
        public async Task DisplayFormatFor_FieldSymbol_WithConstantValue()
        {
            var response = await GetTypeLookUpResponse(line: 19, column: 41);
            AssertContents(response.Sections, "(constant) int Foo.foo = 1");
        }

        [Fact]
        public async Task DisplayFormatFor_EnumValue()
        {
            var response = await GetTypeLookUpResponse(line: 31, column: 23);
        }

        [Fact]
        public async Task DisplayFormatFor_PropertySymbol()
        {
            var response = await GetTypeLookUpResponse(line: 13, column: 38);
            AssertContents(response.Sections, "Foo2 Foo.SomeProperty { get; }");
        }

        [Fact]
        public async Task DisplayFormatFor_PropertySymbol_WithGenerics()
        {
            var response = await GetTypeLookUpResponse(line: 15, column: 70);
            AssertContents(response.Sections, "IDictionary<string, IEnumerable<int>> Foo.SomeDict { get; }");
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
            AssertContents(response.Sections, "bool testissue.Compare(int gameObject, string tagName)",
                new QuickInfoResponseSection { IsCSharpCode = false, Text = "You may have some additional information about this class here." });
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
            AssertContents(response.Sections, "bool testissue.Compare(int gameObject, string tagName)", "Checks if object is tagged with the tag.");
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
            AssertContents(response.Sections, "bool testissue.Compare(int gameObject, string tagName)",
                new QuickInfoResponseSection { IsCSharpCode = false, Text = "Returns:\n\n  Returns true if object is tagged with tag." });
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
            AssertContents(response.Sections, "bool testissue.Compare(int gameObject, string tagName)");
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
            AssertContents(response.Sections, "bool testissue.Compare(int gameObject, string tagName)",
                new QuickInfoResponseSection { IsCSharpCode = false, Text = "Exceptions:\n\n  A\n\n  B" });
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
            AssertContents(response.Sections, "bool testissue.Compare(int gameObject, string tagName)");
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
            AssertContents(response.Sections, "T[] TestClass.mkArray<T>(int n, List<X> list)",
                "Creates a new array of arbitrary type `T` and adds the elements of incoming list to it if possible");
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
            AssertContents(response.Sections, "T in TestClass.mkArray<T>",
                "The element type of the array");
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
            Assert.Empty(response.Sections);
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
            AssertContents(response.Sections, "string Employee.Name { }", "The Name property represents the employee's name.",
                new QuickInfoResponseSection { IsCSharpCode = false, Text = "Value:\n\n  The Name property gets/sets the value of the string field, _name." });
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
            AssertContents(response.Sections, "void TestClass.DoWork(int Int1)", "DoWork is a method in the TestClass class. `System.Console.WriteLine(string)` for information about output statements.");
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
            AssertContents(response.Sections, "T[] TestClass.mkArray<T>(int n)", "Creates a new array of arbitrary type `T`");
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
            AssertContents(response.Sections, "int TestClass.GetZero()");
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
            AssertContents(response.Sections, "void TestClass.DoWork(int Int1)", "DoWork is a method in the TestClass class.\n\n\n\nHere's how you could make a second paragraph in a description.");
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
            AssertContents(response.Sections, "void TestClass.DoWork(int Int1)", "DoWork is a method in the TestClass class. `TestClass.Main()`");
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
            AssertContents(response.Sections, "bool testissue.Compare(int gameObject, string tagName)", "Checks if object is tagged with the tag.");
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
            AssertContents(response.Sections, "T[] testissue.Compare(int gameObject)", "Checks if object is tagged with the tag.",
                new QuickInfoResponseSection { IsCSharpCode = false, Text = "You may have some additional information about this class here." },
                new QuickInfoResponseSection { IsCSharpCode = false, Text = "Returns:\n\n  Returns an array of type `T`." },
                new QuickInfoResponseSection { IsCSharpCode = false, Text = "Exceptions:\n\n  `System.Exception`" });
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
            AssertContents(response.Sections, "void TestClass.DoWork(int Int1)", "DoWork is a method in the TestClass class.");
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
            AssertContents(response.Sections, "(parameter) int gameObject", "The game object.");
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
            AssertContents(response.Sections, "(parameter) string tagName", "Name of the tag.");
        }

        [Fact]
        public async Task AnonymousTypeSubstitution()
        {
            string content = @"
class C
{
    void M1<T>(T t) {}
    void M2()
    {
        var a = new { X = 1, Y = 2 };
        M$$1(a);
    }
}";
            var response = await GetTypeLookUpResponse(content);
            AssertContents(response.Sections, "void C.M1<'a>('a t)",
                new QuickInfoResponseSection { IsCSharpCode = false, Text = "Anonymous Types:" },
                new QuickInfoResponseSection { IsCSharpCode = true, Text = $"    'a is new {{ int X, int Y }}" });
        }

        [Fact]
        public async Task InheritDoc()
        {
            string content = @"
class Program
{
	/// <summary>Hello World</summary>
	public static void A() { }

	/// <inheritdoc cref=""A""/>
    public static void B() { }

    public static void Main()
    {
        A();
        B$$();
    }
}";
            var response = await GetTypeLookUpResponse(content);
            AssertContents(response.Sections, "void Program.B()", "Hello World");
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

        private void AssertContents(ImmutableArray<QuickInfoResponseSection> actual, string description, params QuickInfoResponseSection[] otherSections)
        {
            AssertContents(actual, description, summary: null, otherSections);
        }

        private void AssertContents(ImmutableArray<QuickInfoResponseSection> actual, string description, string? summary = null, params QuickInfoResponseSection[] otherSections)
        {
            var expected = new List<QuickInfoResponseSection>();
            expected.Add(new QuickInfoResponseSection { IsCSharpCode = true, Text = description });
            if (summary is object)
            {
                expected.Add(new QuickInfoResponseSection { IsCSharpCode = false, Text = summary });
            }

            expected.AddRange(otherSections);

            Assert.Equal(expected, actual);
        }
    }
}
