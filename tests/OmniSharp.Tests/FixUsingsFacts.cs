using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using OmniSharp.Models;
using Xunit;

namespace OmniSharp.Tests
{
    public class FixUsingsFacts
    {

        string fileName = "test.cs";

        [Fact]
        public async Task FixUsings_AddsUsingSingle()
        {
            const string fileContents = @"namespace nsA {
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
            string expectedFileContents = @"using nsA;" + 
            Environment.NewLine +
@"namespace nsA {
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

            await AssertBufferContents(fileContents, expectedFileContents);
        }

        [Fact]
        public async Task FixUsings_AddsUsingSingleForFrameworkMethod()
        {
            const string fileContents = @"namespace OmniSharp {
    public class class1 
    {
        public void method1()()
        {
            Console.WriteLine(""abc"");
        }
    }
}";
            string expectedFileContents = @"using System;"  + 
            Environment.NewLine + 
@"namespace OmniSharp {
    public class class1 
    {
        public void method1()()
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
            const string fileContents = @"namespace OmniSharp {
    public class class1 
    {
        public void method1()()
        {
            var s = new StringBuilder();
        }
    }
}";
            string expectedFileContents = @"using System.Text;"  + 
            Environment.NewLine + 
@"namespace OmniSharp {
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
            const string fileContents = @"namespace nsA {
    public class classX{}
}

namespace nsB {
    public class classY{}
}

namespace OmniSharp {
    public class class1 
    {
        public method1()
        {
            var c1 = new classX();
            var c2 = new classY();
        }
    }
}";
            string expectedFileContents = @"using nsA;" + 
            Environment.NewLine +
@"using nsB;"  + 
Environment.NewLine + 
@"namespace nsA {
    public class classX{}
}

namespace nsB {
    public class classY{}
}

namespace OmniSharp {
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
            const string fileContents = @"namespace OmniSharp {
    public class class1 
    {
        public void method1()
        {
            Console.WriteLine(""abc"");
            var sb = new StringBuilder();
        }
    }
}";
            string expectedFileContents = @"using System;" + 
            Environment.NewLine + 
@"using System.Text;"  + 
Environment.NewLine +
@"namespace OmniSharp {
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
            string expectedFileContents = @"using nsA;"  + 
            Environment.NewLine + 
@"namespace nsA {
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
        public async Task FixUsings_AddsUsingLinq()
        {
            const string fileContents = @"namespace OmniSharp {
    public class class1 
    {
        public void method1()
        {
            List<string> first = new List<string>();
            var testing = first.Where(s => s == ""abc"");
        }
    }
}";
            string expectedFileContents = @"using System.Collections.Generic;"  + 
            Environment.NewLine +
@"using System.Linq;" + 
Environment.NewLine +
@"namespace OmniSharp {
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


        private async Task AssertBufferContents(string fileContents, string expectedFileContents)
        {
            var response = await RunFixUsings(fileContents);
            Assert.Equal(response.Buffer, expectedFileContents);
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
            var controller = new OmnisharpController(workspace, null);
            var request = new Request
            {
                FileName = fileName,
                Buffer = fileContents
            };
            return await controller.FixUsings(request);
        }
    }
}
