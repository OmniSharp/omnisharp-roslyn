using System.Linq;
using System.Threading.Tasks;
using OmniSharp.Models;
using OmniSharp.Models.FixUsings;
using OmniSharp.Roslyn.CSharp.Services.Refactoring;
using TestUtility;
using Xunit;
using Xunit.Abstractions;

namespace OmniSharp.Roslyn.CSharp.Tests
{
    public class FixUsingsFacts : AbstractSingleRequestHandlerTestFixture<FixUsingService>
    {
        private const string TestFileName = "test.cs";

        public FixUsingsFacts(ITestOutputHelper output, SharedOmniSharpHostFixture sharedOmniSharpHostFixture)
            : base(output, sharedOmniSharpHostFixture)
        {
        }

        protected override string EndpointName => OmniSharpEndpoints.FixUsings;

        [Fact]
        public async Task FixUsings_AddsUsingSingle()
        {
            const string code = @"
namespace nsA
{
    public class classX{}
}

namespace OmniSharp
{
    public class class1
    {
        public method1()
        {
            var c1 = new classX();
        }
    }
}";

            const string expectedCode = @"
using nsA;

namespace nsA
{
    public class classX{}
}

namespace OmniSharp
{
    public class class1
    {
        public method1()
        {
            var c1 = new classX();
        }
    }
}";

            await AssertBufferContentsAsync(code, expectedCode);
        }

        [Fact]
        public async Task FixUsings_AddsUsingSingleForFrameworkMethod()
        {
            const string code = @"
namespace OmniSharp
{
    public class class1
    {
        public void method1()
        {
            Guid.NewGuid();
        }
    }
}";

            string expectedCode = @"
using System;

namespace OmniSharp
{
    public class class1
    {
        public void method1()
        {
            Guid.NewGuid();
        }
    }
}";

            await AssertBufferContentsAsync(code, expectedCode);
        }

        [Fact]
        public async Task FixUsings_AddsUsingSingleForFrameworkClass()
        {
            const string code = @"
namespace OmniSharp
{
    public class class1
    {
        public void method1()()
        {
            var s = new StringBuilder();
        }
    }
}";

            const string expectedCode = @"
using System.Text;

namespace OmniSharp
{
    public class class1
    {
        public void method1()()
        {
            var s = new StringBuilder();
        }
    }
}";

            await AssertBufferContentsAsync(code, expectedCode);
        }

        [Fact]
        public async Task FixUsings_AddsUsingMultiple()
        {
            const string code = @"
namespace nsA
{
    public class classX{}
}

namespace nsB
{
    public class classY{}
}

namespace OmniSharp
{
    public class class1
    {
        public method1()
        {
            var c1 = new classX();
            var c2 = new classY();
        }
    }
}";

            const string expectedCode = @"
using nsA;
using nsB;

namespace nsA
{
    public class classX{}
}

namespace nsB
{
    public class classY{}
}

namespace OmniSharp
{
    public class class1
    {
        public method1()
        {
            var c1 = new classX();
            var c2 = new classY();
        }
    }
}";

            await AssertBufferContentsAsync(code, expectedCode);
        }

        [Fact]
        public async Task FixUsings_AddsUsingMultipleForFramework()
        {
            const string code = @"
namespace OmniSharp
{
    public class class1
    {
        public void method1()
        {
            Guid.NewGuid();
            var sb = new StringBuilder();
        }
    }
}";

            const string expectedCode = @"
using System;
using System.Text;

namespace OmniSharp
{
    public class class1
    {
        public void method1()
        {
            Guid.NewGuid();
            var sb = new StringBuilder();
        }
    }
}";

            await AssertBufferContentsAsync(code, expectedCode);
        }

        [Fact]
        public async Task FixUsings_ReturnsAmbiguousResult()
        {
            const string code = @"
namespace nsA
{
    public class classX{}
}

namespace nsB
{
    public class classX{}
}

namespace OmniSharp
{
    public class class1
    {
        public method1()
        {
            var c1 = new $$classX();
        }
    }
}";
            var content = TestContent.Parse(code);
            var point = content.GetPointFromPosition();

            var expectedUnresolved = new[]
            {
                new QuickFix()
                {
                    Line = point.Line,
                    Column = point.Offset,
                    FileName = TestFileName,
                    Text = "`classX` is ambiguous. Namespaces: using nsA; using nsB;",
                }
            };

            await AssertUnresolvedReferencesAsync(content.Code, expectedUnresolved);
        }

        [Fact]
        public async Task FixUsings_ReturnsNoUsingsForAmbiguousResult()
        {
            const string code = @"
namespace nsA {
    public class classX{}
}

namespace nsB {
    public class classX{}
}

namespace OmniSharp {
    public class class1
    {
        public method1()
        {
            var c1 = new classX();
        }
    }
}";

            await AssertBufferContentsAsync(code, expectedCode: code);
        }

        [Fact]
        public async Task FixUsings_AddsUsingForExtension()
        {
            const string code = @"
namespace nsA {
    public static class StringExtension {
        public static void Whatever(this string astring) {}
    }
}

namespace OmniSharp {
    public class class1
    {
        public method1()
        {
            ""string"".Whatever();
        }
    }
}";

            const string expectedCode = @"
using nsA;

namespace nsA {
    public static class StringExtension {
        public static void Whatever(this string astring) {}
    }
}

namespace OmniSharp {
    public class class1
    {
        public method1()
        {
            ""string"".Whatever();
        }
    }
}";

            await AssertBufferContentsAsync(code, expectedCode);
        }

        [Fact]
        public async Task FixUsings_AddsUsingLinqMethodSyntax()
        {
            const string code = @"namespace OmniSharp
{
    public class class1
    {
        public void method1()
        {
            List<string> first = new List<string>();
            var testing = first.Where(s => s == ""abc"");
        }
    }
}";

            const string expectedCode = @"using System.Collections.Generic;
using System.Linq;

namespace OmniSharp
{
    public class class1
    {
        public void method1()
        {
            List<string> first = new List<string>();
            var testing = first.Where(s => s == ""abc"");
        }
    }
}";

            await AssertBufferContentsAsync(code, expectedCode);
        }

        [Fact]
        public async Task FixUsings_AddsUsingLinqQuerySyntax()
        {
            const string code = @"namespace OmniSharp
{
    public class class1
    {
        public void method1()
        {
            int[] numbers = { 5, 4, 1, 3, 9, 8, 6, 7, 2, 0 };
            var lowNums =
                from n in numbers
                where n < 5
                select n;
        }
     }
}";

            const string expectedCode = @"using System.Linq;
namespace OmniSharp
{
    public class class1
    {
        public void method1()
        {
            int[] numbers = { 5, 4, 1, 3, 9, 8, 6, 7, 2, 0 };
            var lowNums =
                from n in numbers
                where n < 5
                select n;
        }
     }
}";

            await AssertBufferContentsAsync(code, expectedCode);
        }

        [Fact]
        public async Task FixUsings_RemoveDuplicateUsing()
        {
            const string code = @"
using System;
using System;

namespace OmniSharp
{
    public class class1
    {
        public void method1()
        {
            Guid.NewGuid();
        }
    }
}";

            const string expectedCode = @"
using System;

namespace OmniSharp
{
    public class class1
    {
        public void method1()
        {
            Guid.NewGuid();
        }
    }
}";

            await AssertBufferContentsAsync(code, expectedCode);
        }

        [Fact]
        public async Task FixUsings_RemoveUnusedUsing()
        {
            const string code = @"
using System;
using System.Linq;

namespace OmniSharp
{
    public class class1
    {
        public void method1()
        {
            Guid.NewGuid();
        }
    }
}";

            const string expectedCode = @"
using System;

namespace OmniSharp
{
    public class class1
    {
        public void method1()
        {
            Guid.NewGuid();
        }
    }
}";

            await AssertBufferContentsAsync(code, expectedCode);
        }

        private async Task AssertBufferContentsAsync(string code, string expectedCode)
        {
            var response = await RunFixUsingsAsync(code);
            Assert.Equal(FlattenNewLines(expectedCode), FlattenNewLines(response.Buffer));
        }

        private static string FlattenNewLines(string input)
        {
            return input.Replace("\r\n", "\n");
        }

        private async Task AssertUnresolvedReferencesAsync(string code, QuickFix[] expectedResults)
        {
            var response = await RunFixUsingsAsync(code);
            var results = response.AmbiguousResults.ToArray();

            Assert.Equal(results.Length, expectedResults.Length);

            for (var i = 0; i < results.Length; i++)
            {
                var result = results[i];
                var expectedResult = expectedResults[i];

                Assert.Equal(expectedResult.Line, result.Line);
                Assert.Equal(expectedResult.Column, result.Column);
                Assert.Equal(expectedResult.FileName, result.FileName);
                Assert.Equal(expectedResult.Text, result.Text);
            }
        }

        private async Task<FixUsingsResponse> RunFixUsingsAsync(string code)
        {
            SharedOmniSharpTestHost.AddFilesToWorkspace(new TestFile(TestFileName, code));
            var requestHandler = GetRequestHandler(SharedOmniSharpTestHost);
            var request = new FixUsingsRequest
            {
                FileName = TestFileName
            };

            return await requestHandler.Handle(request);
        }
    }
}
