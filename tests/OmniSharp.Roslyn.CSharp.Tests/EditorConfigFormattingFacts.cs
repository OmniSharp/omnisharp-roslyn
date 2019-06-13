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
        public async Task FormatRespectsDefaultIndentationSize()
        {
            var testFile = new TestFile("dummy.cs", "namespace Bar\n{\nclass Foo {}\n}");

            using (var host = CreateOmniSharpHost(testFile))
            {
                var omnisharpOptions = new OmniSharpOptions();
                omnisharpOptions.FormattingOptions.EnableEditorConfigSupport = true;
                
                var requestHandler = host.GetRequestHandler<CodeFormatService>(OmniSharpEndpoints.CodeFormat);

                var request = new CodeFormatRequest { FileName = testFile.FileName };
                var response = await requestHandler.Handle(request);

                Assert.Equal("namespace Bar\n{\n    class Foo { }\n}", response.Buffer);
            }
        }
    }
}
