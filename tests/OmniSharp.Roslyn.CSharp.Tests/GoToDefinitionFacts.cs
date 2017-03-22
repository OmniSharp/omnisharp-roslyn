using System.Linq;
using System.Threading.Tasks;
using OmniSharp.Models;
using OmniSharp.Roslyn.CSharp.Services.Navigation;
using TestUtility;
using Xunit;
using Xunit.Abstractions;

namespace OmniSharp.Roslyn.CSharp.Tests
{
    public class GoToDefinitionFacts : AbstractSingleRequestHandlerTestFixture<GotoDefinitionService>
    {
        public GoToDefinitionFacts(ITestOutputHelper output)
            : base(output)
        {
        }

        protected override string EndpointName => OmnisharpEndpoints.GotoDefinition;

        [Fact]
        public async Task ReturnsDefinitionInSameFile()
        {
            var testFile = new TestFile("foo.cs", @"
class {|def:Foo|} {
    private F$$oo foo;
}");

            await TestGoToSourceAsync(testFile);
        }

        [Fact]
        public async Task ReturnsDefinitionInDifferentFile()
        {
            var testFile1 = new TestFile("foo.cs", @"
using System;
class {|def:Foo|} {
}");
            var testFile2 = new TestFile("bar.cs", @"
class Bar {
    private F$$oo foo;
}");

            await TestGoToSourceAsync(testFile1, testFile2);
        }

        [Fact]
        public async Task ReturnsEmptyResultWhenDefinitionIsNotFound()
        {
            var testFile1 = new TestFile("foo.cs", @"
using System;
class Foo {
}");
            var testFile2 = new TestFile("bar.cs", @"
class Bar {
    private B$$az foo;
}");

            await TestGoToSourceAsync(testFile1, testFile2);
        }

        [Fact]
        public async Task ReturnsDefinitionInMetadata_WhenSymbolIsStaticMethod()
        {
            var testFile = new TestFile("bar.cs", @"
using System;
class Bar {
    public void Baz() {
        Guid.NewG$$uid();
    }
}");

            await TestGoToMetadataAsync(testFile,
                expectedAssemblyName: AssemblyHelpers.CorLibName,
                expectedTypeName: "System.Guid");
        }

        [Fact]
        public async Task ReturnsDefinitionInMetadata_WhenSymbolIsInstanceMethod()
        {
            var testFile = new TestFile("bar.cs", @"
using System.Collections.Generic;
class Bar {
    public void Baz() {
        var foo = new List<string>();
        foo.ToAr$$ray();
    }
}");

            await TestGoToMetadataAsync(testFile,
                expectedAssemblyName: AssemblyHelpers.CorLibName,
                expectedTypeName: "System.Collections.Generic.List`1");
        }

        [Fact]
        public async Task ReturnsDefinitionInMetadata_WhenSymbolIsGenericType()
        {
            var testFile = new TestFile("bar.cs", @"
using System.Collections.Generic;
class Bar {
    public void Baz() {
        var foo = new Li$$st<string>();
        foo.ToArray();
    }
}");

            await TestGoToMetadataAsync(testFile,
                expectedAssemblyName: AssemblyHelpers.CorLibName,
                expectedTypeName: "System.Collections.Generic.List`1");
        }

        [Fact]
        public async Task ReturnsDefinitionInMetadata_WhenSymbolIsType()
        {
            var testFile = new TestFile("bar.cs", @"
using System;
class Bar {
    public void Baz() {
        var str = Stri$$ng.Empty;
    }
}");

            await TestGoToMetadataAsync(testFile,
                expectedAssemblyName: AssemblyHelpers.CorLibName,
                expectedTypeName: "System.String");
        }

        private async Task TestGoToSourceAsync(params TestFile[] testFiles)
        {
            var response = await GetResponseAsync(testFiles, wantMetadata: false);

            var target = testFiles.FirstOrDefault(tf => tf.Content.GetSpans("def").Count > 0);
            if (target != null)
            {
                var definitionSpan = target.Content.GetSpans("def").First();
                var definitionRange = target.Content.GetRangeFromSpan(definitionSpan);

                Assert.Equal(target.FileName, response.FileName);
                Assert.Equal(definitionRange.Start.Line, response.Line);
                Assert.Equal(definitionRange.Start.Offset, response.Column);
            }
            else
            {
                Assert.Null(response.FileName);
                Assert.Equal(0, response.Line);
                Assert.Equal(0, response.Column);
            }
        }

        private async Task TestGoToMetadataAsync(TestFile testFile, string expectedAssemblyName, string expectedTypeName)
        {
            var response = await GetResponseAsync(new[] { testFile }, wantMetadata: true);

            Assert.NotNull(response.MetadataSource);
            Assert.Equal(expectedAssemblyName, response.MetadataSource.AssemblyName);
            Assert.Equal(expectedTypeName, response.MetadataSource.TypeName);

            // We probably shouldn't hard code metadata locations (they could change randomly)
            Assert.NotEqual(0, response.Line);
            Assert.NotEqual(0, response.Column);
        }

        private async Task<GotoDefinitionResponse> GetResponseAsync(TestFile[] testFiles, bool wantMetadata)
        {
            using (var host = CreateOmniSharpHost(testFiles))
            {
                var source = testFiles.Single(tf => tf.Content.HasPosition);
                var point = source.Content.GetPointFromPosition();

                var request = new GotoDefinitionRequest
                {
                    FileName = source.FileName,
                    Line = point.Line,
                    Column = point.Offset,
                    Timeout = 60000,
                    WantMetadata = wantMetadata
                };

                var requestHandler = GetRequestHandler(host);
                return await requestHandler.Handle(request);
            }
        }
    }
}
