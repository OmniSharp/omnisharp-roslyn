using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Diagnostics;
using OmniSharp.Models.GotoTypeDefinition;
using OmniSharp.Models.Metadata;
using OmniSharp.Models.v1.SourceGeneratedFile;
using OmniSharp.Roslyn.CSharp.Services.Navigation;
using TestUtility;
using Xunit;

namespace OmniSharp.Roslyn.CSharp.Tests
{
    public class GotoTypeDefinitionMetadataFacts : GotoTypeDefinitionTestBase
    {
        public GotoTypeDefinitionMetadataFacts(ITestOutputHelper output, SharedOmniSharpHostFixture sharedOmniSharpHostFixture)
            : base(output, sharedOmniSharpHostFixture)
        {
        }

        [Theory]
        [InlineData("bar.cs")]
        [InlineData("bar.csx")]
        public async Task ReturnsDefinitionInMetadata_WhenSymbolIsStaticMethod(string filename)
        {
            var testFile = new TestFile(filename, @"
using System;
class Bar {
    public void Baz() {
        var gu$$id = Guid.NewGuid();
    }
}");

            await TestGoToMetadataAsync(testFile,
                expectedAssemblyName: AssemblyHelpers.CorLibName,
                expectedTypeName: "System.Guid");
        }

        [Theory]
        [InlineData("bar.cs")]
        [InlineData("bar.csx")]
        public async Task ReturnsDecompiledDefinition_WhenSymbolIsStaticMethod(string filename)
        {
            var testFile = new TestFile(filename, @"
using System;
class Bar {
    public void Baz() {
        var g$$ = Guid.NewGuid();
    }
}");

            await TestDecompilationAsync(testFile,
                expectedAssemblyName: AssemblyHelpers.CorLibName,
                expectedTypeName: "System.Guid");
        }


        [Theory]
        [InlineData("bar.cs")]
        [InlineData("bar.csx")]
        public async Task ReturnsDefinitionInMetadata_WhenSymbolIsParam(string filename)
        {
            var testFile = new TestFile(filename, @"
        using System.Collections.Generic;
        class Bar {
            public void Baz(List<string> par$$am1) {
                var foo = new List<string>();
                var f = param1;
            }
        }");

            await TestGoToMetadataAsync(testFile,
                expectedAssemblyName: AssemblyHelpers.CorLibName,
                expectedTypeName: "System.Collections.Generic.List`1");
        }

        [Theory]
        [InlineData("bar.cs")]
        [InlineData("bar.csx")]
        public async Task ReturnsDecompiledDefinition_WhenSymbolIsIndexedList(string filename)
        {
            var testFile = new TestFile(filename, @"
using System.Collections.Generic;
class Bar {
    public void Baz() {
        var foo = new List<string>();
        var lorem = fo$$o[0];
    }
}");

            await TestDecompilationAsync(testFile,
                expectedAssemblyName: AssemblyHelpers.CorLibName,
                expectedTypeName: "System.Collections.Generic.List`1");
        }

        [Theory]
        [InlineData("bar.cs")]
        [InlineData("bar.csx")]
        public async Task ReturnsDefinitionInMetadata_WhenSymbolIsType(string filename)
        {
            var testFile = new TestFile(filename, @"
        using System;
        class Bar {
            public void Baz() {
                var str = String.Em$$pty;
            }
        }");

            await TestGoToMetadataAsync(testFile,
                expectedAssemblyName: AssemblyHelpers.CorLibName,
                expectedTypeName: "System.String");
        }

        [Theory]
        [InlineData("bar.cs")]
        [InlineData("bar.csx")]
        public async Task ReturnsDefinitionInMetadata_FromMetadata_WhenSymbolIsType(string filename)
        {
            var testFile = new TestFile(filename, @"
using System;
class Bar {
    public void Baz() {
        var num$$ber = int.MaxValue;
    }
}");

            using (var host = CreateOmniSharpHost(testFile))
            {
                var point = testFile.Content.GetPointFromPosition();

                // 1. start by asking for definition of "int"
                var gotoDefinitionRequest = CreateRequest(testFile.FileName, point.Line, point.Offset, wantMetadata: true, timeout: 600000000);
                var gotoDefinitionRequestHandler = GetRequestHandler(host);
                var gotoDefinitionResponse = await gotoDefinitionRequestHandler.Handle(gotoDefinitionRequest);
                var gotoDefinitionResponseMetadataSource = GetMetadataSource(gotoDefinitionResponse);
                Assert.NotNull(gotoDefinitionResponseMetadataSource);
                Assert.Equal(AssemblyHelpers.CorLibName, gotoDefinitionResponseMetadataSource.AssemblyName);
                Assert.Equal("System.Int32", gotoDefinitionResponseMetadataSource.TypeName);
                var info = GetInfo(gotoDefinitionResponse);
                Assert.NotEqual(0, info.Single().Line);
                Assert.NotEqual(0, info.Single().Column);
            }
        }

        [Theory]
        [InlineData("bar.cs")]
        [InlineData("bar.csx")]
        public async Task ReturnsDecompiledDefinition_FromMetadata_WhenSymbolIsType(string filename)
        {
            var testFile = new TestFile(filename, @"
using System;
class Bar {
    public void Baz() {
        var num$$ber = int.MaxValue;
    }
}");

            using var host = CreateOmniSharpHost(new[] { testFile }, new Dictionary<string, string>
            {
                ["RoslynExtensionsOptions:EnableDecompilationSupport"] = "true"
            });

            var point = testFile.Content.GetPointFromPosition();

            // 1. start by asking for definition of "int"
            var gotoDefinitionRequest = CreateRequest(testFile.FileName, point.Line, point.Offset, wantMetadata: true, timeout: 60000000);
            var gotoDefinitionRequestHandler = GetRequestHandler(host);
            var gotoDefinitionResponse = await gotoDefinitionRequestHandler.Handle(gotoDefinitionRequest);

            // 2. now, based on the response information
            // go to the metadata endpoint, and ask for "int" specific decompiled source
            var metadataSource = GetMetadataSource(gotoDefinitionResponse);
            var metadataRequest = new MetadataRequest
            {
                AssemblyName = metadataSource!.AssemblyName,
                TypeName = metadataSource.TypeName,
                ProjectName = metadataSource.ProjectName,
                Language = metadataSource.Language,
                Timeout = 6000000
            };
            var metadataRequestHandler = host.GetRequestHandler<MetadataService>(OmniSharpEndpoints.Metadata);
            var metadataResponse = await metadataRequestHandler.Handle(metadataRequest);

            // 3. the response contains SourceName ("file") and SourceText (syntax tree)
            // use the source to locate "IComparable" which is an interface implemented by Int32 struct
            var decompiledTree = CSharpSyntaxTree.ParseText(metadataResponse.Source);
            var compilationUnit = decompiledTree.GetCompilationUnitRoot();

            // second comment should indicate we have decompiled
            var comments = compilationUnit.DescendantTrivia().Where(t => t.IsKind(SyntaxKind.SingleLineCommentTrivia)).ToArray();
            Assert.NotNull(comments);
            Assert.Equal("// Decompiled with ICSharpCode.Decompiler 11.1.0.9782+a6909b2e1062d2404a52711926fa74030bd96119", comments[1].ToString());

            // contrary to regular metadata, we should have methods with full bodies
            // this condition would fail if decompilation wouldn't work
            var methods = compilationUnit.
                DescendantNodesAndSelf().
                OfType<MethodDeclarationSyntax>().
                Where(m => m.Body != null);

            Assert.NotEmpty(methods);
        }
    }
}
