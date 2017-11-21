
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
        private readonly TestFile s_testFile = new TestFile("dummy.cs", @"using System;
namespace hoverXmlDoc
{
    class testissue
    {
    ///<remarks>Checks if object is tagged with the tag.</remarks>
    /// <param name=""gameObject"">The game object.</param> 
    /// <param name=""tagName"">Name of the tag </param>
    /// <returns>Returns <c> true</c>if object is tagged with tag.</returns>

        public static bool C$$ompare(int gameObject, string tagName)
        {
            return gameObject.TagifyCompareTag(tagName);
        }
    }
}
");

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
        public async Task CheckXmlDocumentationRemarksText()
        {
             string content= @"using System;
namespace hoverXmlDoc
{
    class testissue
    {
    ///<remarks>You may have some additional information about this class here.</remarks>
        public static bool C$$ompare(int gameObject, string tagName)
        {
            return gameObject.TagifyCompareTag(tagName);
        }
    }
}
";
            var response = await GetTypeLookUpResponse(content);
            var expected =
            @"Remarks: You may have some additional information about this class here.";
            Assert.Equal(expected, response.DocComment.RemarksText.ToString());
        }

        [Fact]
        public async Task CheckXmlDocumentationSummaryText()
        {
            string content = @"using System;
namespace hoverXmlDoc
{
    class testissue
    {
    ///<summary>Checks if object is tagged with the tag.</summary>
        public static bool C$$ompare(int gameObject, string tagName)
        {
            return gameObject.TagifyCompareTag(tagName);
        }
    }
}
";
            var response = await GetTypeLookUpResponse(content);
            var expected =
            @"Summary: Checks if object is tagged with the tag.";
            Assert.Equal(expected, response.DocComment.SummaryText.ToString());
        }

        [Fact]
        public async Task CheckXmlDocumentationReturnsText()
        {
            string content = @"using System;
namespace hoverXmlDoc
{
    class testissue
    {
    ///<returns>Returns true if object is tagged with tag.</returns>
        public static bool C$$ompare(int gameObject, string tagName)
        {
            return gameObject.TagifyCompareTag(tagName);
        }
    }
}
";

            var response = await GetTypeLookUpResponse(content);
            var expected =
            @"Returns: Returns true if object is tagged with tag.";
            Assert.Equal(expected, response.DocComment.ReturnsText.ToString());
        }

        [Fact]
        public async Task CheckXmlDocumentationExampleText()
        {
            string content = @"using System;
namespace hoverXmlDoc
{
    class testissue
    {
    ///<example>Checks if object is tagged with the tag.</example>
        public static bool C$$ompare(int gameObject, string tagName)
        {
            return gameObject.TagifyCompareTag(tagName);
        }
    }
}
";
            var response = await GetTypeLookUpResponse(content);
            var expected =
            @"Example: Checks if object is tagged with the tag.";
            Assert.Equal(expected, response.DocComment.ExampleText.ToString());
        }

        [Fact]
        public async Task CheckXmlDocumentationExceptionText()
        {
            string content = @"using System;
namespace hoverXmlDoc
{
    class testissue
    {
    ///<exception cref=""System.Exception"">Thrown when something goes wrong</exception>
        public static bool C$$ompare(int gameObject, string tagName)
        {
            return gameObject.TagifyCompareTag(tagName);
        }
    }
}
";
            var response = await GetTypeLookUpResponse(content);
            var expected =
            @"System.Exception: Thrown when something goes wrong";
            Assert.Equal(expected, response.DocComment.ExceptionText.ToString());
        }

        [Fact]
        public async Task CheckXmlDocumentationParameter1()
        {
            string content = @"using System;
namespace hoverXmlDoc
{
    class testissue
    {
    /// <param name=""gameObject"">The game object.</param> 
    /// <param name=""tagName"">Name of the tag </param>
        public static bool C$$ompare(int gameObject, string tagName)
        {
            return gameObject.TagifyCompareTag(tagName);
        }
    }
}
";
            var response = await GetTypeLookUpResponse(content);
            var expected =
            @"gameObject: The game object.";
            Assert.Equal(2,response.DocComment.Param.Count);
            Assert.Equal(expected, response.DocComment.Param[0].ToString());
        }

        [Fact]
        public async Task CheckXmlDocumentationParameter2()
        {
            string content = @"using System;
namespace hoverXmlDoc
{
    class testissue
    {
    /// <param name=""gameObject"">The game object.</param> 
    /// <param name=""tagName"">Name of the tag.</param>
        public static bool C$$ompare(int gameObject, string tagName)
        {
            return gameObject.TagifyCompareTag(tagName);
        }
    }
}
";
            var response = await GetTypeLookUpResponse(content);
            var expected =
            @"tagName: Name of the tag.";
            Assert.Equal(2, response.DocComment.Param.Count);
            Assert.Equal(expected, response.DocComment.Param[1].ToString());
        }

        [Fact]
        public async Task CheckXmlDocumentationTypeParameter()
        {
            string content = @"public class TestClass
{
    /// <summary>
    /// Creates a new array of arbitrary type <typeparamref name=""T""/>
    /// </summary>
    /// <typeparam name=""T"">The element type of the array</typeparam>
            public static T[] m$$kArray<T>(int n)
            {
                return new T[n];
            }
        }
";
            var response = await GetTypeLookUpResponse(content);
            var expected =
            @"T: The element type of the array";
            Assert.Single(response.DocComment.TypeParam);
            Assert.Equal(expected, response.DocComment.TypeParam[0].ToString());
        }

        [Fact]
        public async Task CheckXmlDocumentationValueText()
        {
            string content = @"public class Employee
{
    private string _name;

    /// <summary>The Name property represents the employee's name.</summary>
    /// <value>The Name property gets/sets the value of the string field, _name.</value>
    public string N$$ame
    {
        get
        {
            return _name;
        }
        set
        {
            _name = value;
        }
    }
}
";
            var response = await GetTypeLookUpResponse(content);
            var expected =
            @"Value: The Name property gets/sets the value of the string field, _name.";
            Assert.Equal(expected, response.DocComment.ValueText.ToString());
        }

        [Fact]
        public async Task CheckXmlDocumentationSee()
        {
            string content = @"/// text for class TestClass
public class TestClass
{
    /// <summary>DoWork is a method in the TestClass class. <see cref=""System.Console.WriteLine(System.String)""/> for information about output statements.</summary>
            public static void Do$$Work(int Int1)
            {
            }

            /// text for Main
            static void Main()
            {
            }
        }
";
            var response = await GetTypeLookUpResponse(content);
            var expected =
            @"Summary: DoWork is a method in the TestClass class. System.Console.WriteLine(System.String) for information about output statements.";
            Assert.Equal(expected, response.DocComment.SummaryText.ToString());
        }

    }
}
