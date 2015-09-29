#if DNX451
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using OmniSharp.Models;
using OmniSharp.Options;
using Xunit;
 
namespace OmniSharp.Tests
{ 
    public class FixUsingsFacts
    {
        string fileName = "test.cs";

        [Fact]
        public async Task FixUsings_AddsUsingSingle()
        {
            const string fileContents = @"namespace nsA 
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
            string expectedFileContents = @"using nsA;

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

            await AssertBufferContents(fileContents, expectedFileContents);
        }

        [Fact]
        public async Task FixUsings_AddsUsingSingleForFrameworkMethod()
        {
            const string fileContents = @"namespace OmniSharp
{
    public class class1 
    {
        public void method1()
        {
            Console.WriteLine(""abc"");
        }
    }
}";
            string expectedFileContents = @"using System;

namespace OmniSharp
{
    public class class1 
    {
        public void method1()
        {
            Console.WriteLine(""abc"");
        }
    }
}";

            await AssertBufferContents(fileContents, expectedFileContents);
        }

        [Fact]
        public async Task FixUsings_AddsUsingSingleForFrameworkClass()
        {
            const string fileContents = @"namespace OmniSharp
{
    public class class1 
    {
        public void method1()()
        {
            var s = new StringBuilder();
        }
    }
}";
            string expectedFileContents = @"using System.Text;

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

            await AssertBufferContents(fileContents, expectedFileContents);
        }

        [Fact]
        public async Task FixUsings_AddsUsingMultiple()
        {
            const string fileContents = @"namespace nsA
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
            string expectedFileContents = @"using nsA;
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

            await AssertBufferContents(fileContents, expectedFileContents);
        }

        [Fact]
        public async Task FixUsings_AddsUsingMultipleForFramework()
        {
            const string fileContents = @"namespace OmniSharp
{
    public class class1 
    {
        public void method1()
        {
            Console.WriteLine(""abc"");
            var sb = new StringBuilder();
        }
    }
}";
            string expectedFileContents = @"using System;
using System.Text;

namespace OmniSharp
{
    public class class1 
    {
        public void method1()
        {
            Console.WriteLine(""abc"");
            var sb = new StringBuilder();
        }
    }
}";

            await AssertBufferContents(fileContents, expectedFileContents);
        }

        [Fact]
        public async Task FixUsings_ReturnsAmbiguousResult()
        {
            const string fileContents = @"
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
            var c1 = new $classX();
        }
    }
}";
            var classLineColumn = TestHelpers.GetLineAndColumnFromDollar(TestHelpers.RemovePercentMarker(fileContents));
            var fileContentNoDollarMarker = TestHelpers.RemoveDollarMarker(fileContents);
            var expectedUnresolved = new List<QuickFix>();
            expectedUnresolved.Add(new QuickFix()
                {
                    Line = classLineColumn.Line,
                    Column = classLineColumn.Column,
                    FileName = fileName,
                    Text = "`classX` is ambiguous"

                });
            await AssertUnresolvedReferences(fileContentNoDollarMarker, expectedUnresolved);
        }

        [Fact]
        public async Task FixUsings_ReturnsNoUsingsForAmbiguousResult()
        {
            const string fileContents = @"namespace nsA {
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
            await AssertBufferContents(fileContents, fileContents);
        }

        [Fact]
        public async Task FixUsings_AddsUsingForExtension()
        {
            const string fileContents = @"namespace nsA {
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
            string expectedFileContents = @"using nsA;

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

            await AssertBufferContents(fileContents, expectedFileContents);
        }


        [Fact]
        public async Task FixUsings_AddsUsingLinqMethodSyntax()
        {
            const string fileContents = @"namespace OmniSharp
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
            string expectedFileContents = @"using System.Collections.Generic;
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

            await AssertBufferContents(fileContents, expectedFileContents);
        }

        [Fact]
        public async Task FixUsings_RemoveDuplicateUsing()
        {
            const string fileContents = @"using System;
using System;

namespace OmniSharp
{
    public class class1 
    {
        public void method1()
        {
            Console.WriteLine(""test"");
        }
    }
}";

            const string expectedFileContents = @"using System;

namespace OmniSharp
{
    public class class1 
    {
        public void method1()
        {
            Console.WriteLine(""test"");
        }
    }
}";
            await AssertBufferContents(fileContents, expectedFileContents);
        }

        [Fact]
        public async Task FixUsings_RemoveUnusedUsing()
        {
            const string fileContents = @"using System;
using System.Linq;

namespace OmniSharp
{
    public class class1 
    {
        public void method1()
        {
            Console.WriteLine(""test"");
        }
    }
}";

            const string expectedFileContents = @"using System;

namespace OmniSharp
{
    public class class1 
    {
        public void method1()
        {
            Console.WriteLine(""test"");
        }
    }
}";
            await AssertBufferContents(fileContents, expectedFileContents);
        }

        private async Task AssertBufferContents(string fileContents, string expectedFileContents)
        {
            var response = await RunFixUsings(fileContents);
            Assert.Equal(expectedFileContents, response.Buffer);
        }

        private async Task AssertUnresolvedReferences(string fileContents, List<QuickFix> expectedUnresolved)
        {
            var response = await RunFixUsings(fileContents);
            var qfList = response.AmbiguousResults.ToList();
            Assert.Equal(qfList.Count(), expectedUnresolved.Count());
            var i = 0;
            foreach (var expectedQuickFix in expectedUnresolved)
            {
                Assert.Equal(qfList[i].Line, expectedQuickFix.Line);
                Assert.Equal(qfList[i].Column, expectedQuickFix.Column);
                Assert.Equal(qfList[i].FileName, expectedQuickFix.FileName);
                Assert.Equal(qfList[i].Text, expectedQuickFix.Text);
                i++;
            }
        }

        private async Task<FixUsingsResponse> RunFixUsings(string fileContents)
        {
            var workspace = TestHelpers.CreateSimpleWorkspace(fileContents, fileName);

            var fakeOptions = new FakeOmniSharpOptions();
            fakeOptions.Options = new OmniSharpOptions();
            fakeOptions.Options.FormattingOptions = new FormattingOptions() {NewLine = "\n"};
            var controller = new OmnisharpController(workspace, fakeOptions);
            var request = new FixUsingsRequest
            {
                FileName = fileName,
                Buffer = fileContents
            };
            return await controller.FixUsings(request);
        }
    }
}
#endif
