using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;
using OmniSharp.Models.CodeFormat;
using OmniSharp.Options;
using OmniSharp.Roslyn.CSharp.Services.Formatting;
using TestUtility;
using Xunit;
using Xunit.Abstractions;

namespace OmniSharp.Roslyn.CSharp.Tests
{
    public class EditorConfigFormattingFacts : AbstractTestFixture
    {
        public EditorConfigFormattingFacts(ITestOutputHelper output, SharedOmniSharpHostFixture sharedOmniSharpHostFixture)
            : base(output, sharedOmniSharpHostFixture)
        {
        }

        [Fact]
        public async Task FormatRespectsDefaultSettings()
        {
            var testFile = new TestFile("dummy.cs", "class Foo\n{\n public Foo()\n}\n}");
            var expected = "class Foo\n{\n    public Foo()\n}\n}";

            using (var host = CreateOmniSharpHost(new[] { testFile }, new Dictionary<string, string> { ["FormattingOptions:EnableEditorConfigSupport"] = "true" }))
            {
                var requestHandler = host.GetRequestHandler<CodeFormatService>(OmniSharpEndpoints.CodeFormat);

                var request = new CodeFormatRequest { FileName = testFile.FileName };
                var response = await requestHandler.Handle(request);

                Assert.Equal(expected, response.Buffer);
            }
        }

        [Fact]
        public async Task FormatRespectsSharedSettings()
        {
            var testFile = new TestFile(Path.Combine(TestAssets.Instance.TestFilesFolder, "dummy.cs"), "class Foo\n{\n    public Foo()\n}\n}");
            var expected = "class Foo\n{\n public Foo()\n}\n}";

            using (var host = CreateOmniSharpHost(new[] { testFile }, new Dictionary<string, string> { ["FormattingOptions:EnableEditorConfigSupport"] = "true" }))
            {
                var requestHandler = host.GetRequestHandler<CodeFormatService>(OmniSharpEndpoints.CodeFormat);

                var request = new CodeFormatRequest { FileName = testFile.FileName };
                var response = await requestHandler.Handle(request);

                Assert.Equal(expected, response.Buffer);
            }
        }

        [Fact]
        public async Task FormatRespectsCSharpSettings()
        {
            var testFile = new TestFile(Path.Combine(TestAssets.Instance.TestFilesFolder, "dummy.cs"), @"
class Foo { }
class Bar : Foo { }
");
            var expected = @"
class Foo { }
class Bar:Foo { }
";

            using (var host = CreateOmniSharpHost(new[] { testFile }, new Dictionary<string, string> { ["FormattingOptions:EnableEditorConfigSupport"] = "true" }))
            {
                var requestHandler = host.GetRequestHandler<CodeFormatService>(OmniSharpEndpoints.CodeFormat);

                var request = new CodeFormatRequest { FileName = testFile.FileName };
                var response = await requestHandler.Handle(request);

                Assert.Equal(expected, response.Buffer);
            }
        }
    }
}
