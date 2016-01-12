using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;
using Microsoft.Extensions.PlatformAbstractions;
using OmniSharp.Models;
using OmniSharp.Options;
using OmniSharp.Roslyn.CSharp.Services.Refactoring;
using OmniSharp.Services;
using OmniSharp.Tests;
using Xunit;

namespace OmniSharp.Roslyn.CSharp.Tests
{
    public class FixUsingsFacts
    {
        private readonly string fileName = "test.cs";

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
            Guid.NewGuid();
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
            Guid.NewGuid();
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
            Guid.NewGuid();
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
            Guid.NewGuid();
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


        [Fact(Skip = "Need to find a way to load System.Linq in to test host.")]
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
        public async Task FixUsings_AddsUsingLinqQuerySyntax()
        {
            const string fileContents = @"namespace OmniSharp
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
            string expectedFileContents = @"using System.Linq;
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
            Guid.NewGuid();
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
            Guid.NewGuid();
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
            Guid.NewGuid();
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
            Guid.NewGuid();
        }
    }
}";
            await AssertBufferContents(fileContents, expectedFileContents);
        }
        
        private async Task AssertBufferContents(string fileContents, string expectedFileContents)
        {
            var response = await RunFixUsings(fileContents);
            Assert.Equal(FlattenNewLines(expectedFileContents), FlattenNewLines(response.Buffer));
        }

        private string FlattenNewLines(string input) {
            return input.Replace("\r\n", "\n");
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
            var host = TestHelpers.CreatePluginHost(new[] { typeof(FixUsingService).GetTypeInfo().Assembly });
            var workspace = await TestHelpers.CreateSimpleWorkspace(host, fileContents, fileName);

            var fakeOptions = new FakeOmniSharpOptions();
            fakeOptions.Options = new OmniSharpOptions();
            fakeOptions.Options.FormattingOptions = new FormattingOptions() { NewLine = "\n" };
            var providers = host.GetExports<ICodeActionProvider>();
            var controller = new FixUsingService(workspace, providers);
            var request = new FixUsingsRequest
            {
                FileName = fileName,
                Buffer = fileContents
            };
            return await controller.Handle(request);
        }
    }
}
