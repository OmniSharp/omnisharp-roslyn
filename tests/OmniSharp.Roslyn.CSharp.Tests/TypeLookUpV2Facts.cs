
using OmniSharp.Models.v2.TypeLookUp;
using OmniSharp.Roslyn.CSharp.Services.Types.V2;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using TestUtility;
using Xunit;
using Xunit.Abstractions;

namespace OmniSharp.Roslyn.CSharp.Tests
{
    public class TypeLookUpV2Facts : AbstractSingleRequestHandlerTestFixture<TypeLookupService>
    {
        public TypeLookUpV2Facts(ITestOutputHelper testOutput) : base(testOutput)
        {
        }

        protected override string EndpointName => OmniSharpEndpoints.V2.TypeLookup;

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
        public async Task XmlDocumentationRemarksText()
        {
             string content= @"
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
            @"Remarks: You may have some additional information about this class here.";
            Assert.Equal(expected, response.DocComment.RemarksText);
        }

        [Fact]
        public async Task XmlDocumentationSummaryText()
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
            @"Summary: Checks if object is tagged with the tag.";
            Assert.Equal(expected, response.DocComment.SummaryText);
        }

        [Fact]
        public async Task XmlDocumentationReturnsText()
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
            @"Returns: Returns true if object is tagged with tag.";
            Assert.Equal(expected, response.DocComment.ReturnsText);
        }

        [Fact]
        public async Task XmlDocumentationExampleText()
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
            @"Example: Checks if object is tagged with the tag.";
            Assert.Equal(expected, response.DocComment.ExampleText);
        }

        [Fact]
        public async Task XmlDocumentationExceptionText()
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
            Assert.Equal(2,response.DocComment.Exception.Count());

            var expectedException0 =
            @"A: A description";
            Assert.Equal(expectedException0, response.DocComment.Exception[0]);
            var expectedException1 =
            @"B: B description";
            Assert.Equal(expectedException1, response.DocComment.Exception[1]);
        }

        [Fact]
        public async Task XmlDocumentationParameter()
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
            Assert.Equal(2, response.DocComment.ParamElements.Length);

            var expectedParam0 =
            @"gameObject: The game object.";
            Assert.Equal(expectedParam0, response.DocComment.ParamElements[0]);
            var expectedParam1 =
            @"tagName: Name of the tag.";
            Assert.Equal(expectedParam1, response.DocComment.ParamElements[1]);
        }

        [Fact]
        public async Task XmlDocumentationTypeParameter()
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
            Assert.Equal(2,response.DocComment.TypeParamElements.Count());

            var expected0 =
           @"T: The element type of the array";
            Assert.Equal(expected0, response.DocComment.TypeParamElements[0]);
            var expected1 =
           @"X: The element type of the list";
            Assert.Equal(expected1, response.DocComment.TypeParamElements[1]);
        }

        [Fact]
        public async Task XmlDocumentationValueText()
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
            @"Value: The Name property gets/sets the value of the string field, _name.";
            Assert.Equal(expectedValue, response.DocComment.ValueText);
            var expectedSummary =
            @"Summary: The Name property represents the employee's name.";
            Assert.Equal(expectedSummary, response.DocComment.SummaryText);
        }

        [Fact]
        public async Task XmlDocumentationNestedTagSee()
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
            @"Summary: DoWork is a method in the TestClass class. System.Console.WriteLine(System.String) for information about output statements.";
            Assert.Equal(expected, response.DocComment.SummaryText);
        }

        [Fact]
        public async Task XmlDocumentationNestedTagParamRef()
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
            @"Summary: Creates a new array of arbitrary type T ";
            Assert.Equal(expected, response.DocComment.SummaryText);
        }

        [Fact]
        public async Task XmlDocumentationNestedTagCode()
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
            @"Example: This sample shows how to call the TestClass.GetZero method.

    class TestClass 
    {
        static int Main() 
        {
            return GetZero();
        }
    }
    ";
            Assert.Equal(expected.Replace("\r",""), response.DocComment.ExampleText);
        }

        [Fact]
        public async Task XmlDocumentationNestedTagPara()
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
@"Summary: DoWork is a method in the TestClass class.

Here's how you could make a second paragraph in a description.";
            Assert.Equal(expected.Replace("\r", ""), response.DocComment.SummaryText);
        }

        [Fact]
        public async Task XmlDocumentationNestedTagSeeAlso()
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
@"Summary: DoWork is a method in the TestClass class.
See also: TestClass.Main ";
            Assert.Equal(expected.Replace("\r",""), response.DocComment.SummaryText);
        }

        [Fact]
        public async Task XmlDocumentationSummaryAndParam()
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
            @"Summary: Checks if object is tagged with the tag.";
            Assert.Equal(expected, response.DocComment.SummaryText);

            Assert.Equal(2, response.DocComment.ParamElements.Length);
            var expectedParam0 =
            @"gameObject: The game object.";
            Assert.Equal(expectedParam0, response.DocComment.ParamElements[0]);
            var expectedParam1 =
            @"tagName: Name of the tag.";
            Assert.Equal(expectedParam1, response.DocComment.ParamElements[1]);
        }

        [Fact]
        public async Task XmlDocumentationManyTags()
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
            @"Summary: Checks if object is tagged with the tag.";
            Assert.Equal(expectedSummary, response.DocComment.SummaryText);

            Assert.Single(response.DocComment.ParamElements);
            var expectedParam =
            @"gameObject: The game object.";
            Assert.Equal(expectedParam, response.DocComment.ParamElements[0]);

            var expectedExample =
            @"Example: Invoke using A.Compare(5) where A is an instance of the class testissue.";
            Assert.Equal(expectedExample, response.DocComment.ExampleText);

            Assert.Single(response.DocComment.TypeParamElements);
            var expectedTypeParam =
           @"T: The element type of the array";
            Assert.Equal(expectedTypeParam, response.DocComment.TypeParamElements[0]);

            Assert.Single(response.DocComment.Exception);
            var expectedException =
            @"System.Exception: Thrown when something goes wrong";
            Assert.Equal(expectedException, response.DocComment.Exception[0]);

            var expectedRemarks =
            @"Remarks: You may have some additional information about this class here.";
            Assert.Equal(expectedRemarks, response.DocComment.RemarksText);

            var expectedReturns =
            @"Returns: Returns an array of type T .";
            Assert.Equal(expectedReturns, response.DocComment.ReturnsText);
        }
    }
}
