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
using Xunit.Abstractions;

namespace OmniSharp.Roslyn.CSharp.Tests
{
    public class GotoTypeDefinitionFacts : AbstractSingleRequestHandlerTestFixture<GotoTypeDefinitionService>
    {
        public GotoTypeDefinitionFacts(ITestOutputHelper output, SharedOmniSharpHostFixture sharedOmniSharpHostFixture)
           : base(output, sharedOmniSharpHostFixture)
        {
        }
        protected override string EndpointName => OmniSharpEndpoints.GotoTypeDefinition;

        [Theory]
        [InlineData("foo.cs")]
        [InlineData("foo.csx")]
        public async Task ReturnsDefinitionInSameFile(string filename)
        {
            var testFile = new TestFile(filename, @"
class {|def:Foo|} {
    private Foo f$$oo;
}");

            await TestGoToSourceAsync(testFile);
        }

        [Theory]
        [InlineData("foo.cs")]
        [InlineData("foo.csx")]
        public async Task DoesNotReturnOnPropertAccessorGet(string filename)
        {
            var testFile = new TestFile(filename, @"
class Test {
    public string Foo{ g$$et; set; }
}");

            await TestGoToSourceAsync(testFile);
        }

        [Theory]
        [InlineData("foo.cs")]
        [InlineData("foo.csx")]
        public async Task DoesNotReturnOnPropertAccessorSet(string filename)
        {
            var testFile = new TestFile(filename, @"
class Test {
    public int Foo{ get; s$$et; }
}");

            await TestGoToSourceAsync(testFile);
        }

        [Theory]
        [InlineData("foo.cs")]
        [InlineData("foo.csx")]
        public async Task ReturnsOnPropertyAccessor(string filename)
        {
            var testFile = new TestFile(filename, @"
class Test {
    public Bar Foo{ get; set; }
    public class |def:Bar|
    {
        public int lorem { get; set; }
    }
    public static void main()
    {
        F$$oo = 3;
    }
}");

            await TestGoToSourceAsync(testFile);
        }

        [Theory]
        [InlineData("foo.cs")]
        [InlineData("foo.csx")]
        public async Task ReturnsOnPrivateField(string filename)
        {
            var testFile = new TestFile(filename, @"
class Test {

    public Bar foo;
    public class |def:Bar|
    {
        public int lorem { get; set; }
    }
    public int Foo
    {
        get => f$$oo;
        set => foo = value;
    }
}");

            await TestGoToSourceAsync(testFile);
        }

        [Theory]
        [InlineData("foo.cs")]
        [InlineData("foo.csx")]
        public async Task ReturnsOnPropertyAccessorField2(string filename)
        {
            var testFile = new TestFile(filename, @"
class Test {

    public Bar foo { get; set; };
    public class |def:Bar|
    {
        public int lorem { get; set; }
    }
    public int Foo
    {
        get => foo;
        set => f$$oo = value;
    }
}");

            await TestGoToSourceAsync(testFile);
        }

        [Theory]
        [InlineData("foo.cs")]
        [InlineData("foo.csx")]
        public async Task ReturnsOnPropertySetterParam(string filename)
        {
            var testFile = new TestFile(filename, @"
class Test {

    public Bar foo { get; set; }
    public class |def:Bar|
    {
        public int lorem { get; set; }
    }
    public Bar Foo
    {
        get => foo;
        set => foo = va$$lue;
    }
}");

            await TestGoToSourceAsync(testFile);
        }

        [Theory]
        [InlineData("foo.cs")]
        [InlineData("foo.csx")]
        public async Task ReturnsOnPropertyAccessorPropertyGetting(string filename)
        {
            var testFile = new TestFile(filename, @"
class Test {
    public Bar Foo { get; set; }
    public class |def:Bar|
    {
        public int lorem { get; set; }
    }
    public static void main()
    {
        Foo = 3;
        Console.WriteLine(F$$oo);
    }
}");

            await TestGoToSourceAsync(testFile);
        }

        [Theory]
        [InlineData("foo.cs")]
        [InlineData("foo.csx")]
        public async Task ReturnsOnImplicitLambdaParam(string filename)
        {
            var testFile = new TestFile(filename, @"
using System.Collections.Generic;

class Test {
    public Bar Foo { get; set; }
    public class |def:Bar|
    {
        public int lorem { get; set; }
    }
    public static void Main()
    {
        var list = new List<Bar>();
        list.Add(new Bar());
        list.ForEach(inp$$ut => _ = input.lorem);
    }
}");

            await TestGoToSourceAsync(testFile);
        }

        [Theory]
        [InlineData("foo.cs")]
        [InlineData("foo.csx")]
        public async Task ReturnsOnListFindResult(string filename)
        {
            var testFile = new TestFile(filename, @"
using System.Collections.Generic;

class Test {
    public Bar Foo { get; set; }
    public class |def:Bar|
    {
        public int lorem { get; set; }
    }
    public static void Main()
    {
        var list = new List<Bar>();
        list.Add(new Bar());
        var out$$put = list.Find(input => _ = input.lorem == 12);
    }
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
    private Foo f$$oo;
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
            private Baz f$$oo;
        }");

            await TestGoToSourceAsync(testFile1, testFile2);
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
            Assert.Equal("// Decompiled with ICSharpCode.Decompiler 8.2.0.7535", comments[1].ToString());

            // contrary to regular metadata, we should have methods with full bodies
            // this condition would fail if decompilation wouldn't work
            var methods = compilationUnit.
                DescendantNodesAndSelf().
                OfType<MethodDeclarationSyntax>().
                Where(m => m.Body != null);

            Assert.NotEmpty(methods);
        }

        [Fact]
        public async Task ReturnsNoResultsButDoesNotThrowForNamespaces()
        {
            var testFile = new TestFile("foo.cs", "namespace F$$oo {}");
            var response = await GetResponseAsync(new[] { testFile }, wantMetadata: false);
            Assert.Empty(GetInfo(response));
        }

        [Fact]
        public async Task ReturnsResultsForSourceGenerators()
        {
            const string Source = @"
public class {|generatedClassName:Generated|}
{
    public int {|propertyName:Property|} { get; set; }
}
";
            const string FileName = "real.cs";
            TestFile generatedTestFile = new("GeneratedFile.cs", Source);
            var testFile = new TestFile(FileName, @"
class C
{
    public void M(Generated gen)
    {
        _ = ge$$n.Property;
    }
}
");

            TestHelpers.AddProjectToWorkspace(SharedOmniSharpTestHost.Workspace,
                "project.csproj",
                new[] { "net6.0" },
                new[] { testFile },
                analyzerRefs: ImmutableArray.Create<AnalyzerReference>(new TestGeneratorReference(
                    context => context.AddSource("GeneratedFile", generatedTestFile.Content.Code))));

            var point = testFile.Content.GetPointFromPosition();

            var gotoDefRequest = CreateRequest(FileName, point.Line, point.Offset, wantMetadata: true);
            var gotoDefHandler = GetRequestHandler(SharedOmniSharpTestHost);
            var response = await gotoDefHandler.Handle(gotoDefRequest);
            var info = GetInfo(response).Single();

            Assert.NotNull(info.SourceGeneratorInfo);

            var expectedSpan = generatedTestFile.Content.GetSpans("generatedClassName").Single();
            var expectedRange = generatedTestFile.Content.GetRangeFromSpan(expectedSpan);

            Assert.Equal(expectedRange.Start.Line, info.Line);
            Assert.Equal(expectedRange.Start.Offset, info.Column);

            var sourceGeneratedFileHandler = SharedOmniSharpTestHost.GetRequestHandler<SourceGeneratedFileService>(OmniSharpEndpoints.SourceGeneratedFile);
            var sourceGeneratedRequest = new SourceGeneratedFileRequest
            {
                DocumentGuid = info.SourceGeneratorInfo.DocumentGuid,
                ProjectGuid = info.SourceGeneratorInfo.ProjectGuid
            };

            var sourceGeneratedFileResponse = await sourceGeneratedFileHandler.Handle(sourceGeneratedRequest);
            Assert.NotNull(sourceGeneratedFileResponse);
            Assert.Equal(generatedTestFile.Content.Code, sourceGeneratedFileResponse.Source);
            Assert.Equal(@"OmniSharp.Roslyn.CSharp.Tests\OmniSharp.Roslyn.CSharp.Tests.TestSourceGenerator\GeneratedFile.cs", sourceGeneratedFileResponse.SourceName.Replace("/", @"\"));
        }

        protected async Task TestGoToSourceAsync(params TestFile[] testFiles)
        {
            var response = await GetResponseAsync(testFiles, wantMetadata: false);

            var targets =
                from tf in testFiles
                from span in tf.Content.GetSpans("def")
                select (tf, span);

            var info = GetInfo(response);

            if (targets.Any())
            {
                foreach (var (file, definitionSpan) in targets)
                {
                    var definitionRange = file.Content.GetRangeFromSpan(definitionSpan);

                    Assert.Contains(info,
                        def => file.FileName == def.FileName
                               && definitionRange.Start.Line == def.Line
                               && definitionRange.Start.Offset == def.Column);
                }
            }
            else
            {
                Assert.Empty(info);
            }
        }

        protected async Task TestDecompilationAsync(TestFile testFile, string expectedAssemblyName, string expectedTypeName)
        {
            using var host = CreateOmniSharpHost(new[] { testFile }, new Dictionary<string, string>
            {
                ["RoslynExtensionsOptions:EnableDecompilationSupport"] = "true"
            });

            var response = await GetResponseAsync(new[] { testFile }, wantMetadata: true);
            var metadataSource = GetMetadataSource(response);

            Assert.NotNull(metadataSource);
            Assert.NotEmpty(GetInfo(response));
            Assert.Equal(expectedAssemblyName, metadataSource.AssemblyName);
            Assert.Equal(expectedTypeName, metadataSource.TypeName);
        }

        protected async Task TestGoToMetadataAsync(TestFile testFile, string expectedAssemblyName, string expectedTypeName)
        {
            var response = await GetResponseAsync(new[] { testFile }, wantMetadata: true);
            var metadataSource = GetMetadataSource(response);

            var responseInfo = GetInfo(response);
            Assert.NotNull(metadataSource);
            Assert.NotEmpty(responseInfo);
            Assert.Equal(expectedAssemblyName, metadataSource.AssemblyName);
            Assert.Equal(expectedTypeName, metadataSource.TypeName);

            // We probably shouldn't hard code metadata locations (they could change randomly)
            Assert.NotEqual(0, responseInfo.Single().Line);
            Assert.NotEqual(0, responseInfo.Single().Column);
        }

        protected async Task<GotoTypeDefinitionResponse> GetResponseAsync(TestFile[] testFiles, bool wantMetadata)
        {
            SharedOmniSharpTestHost.AddFilesToWorkspace(testFiles);
            var source = testFiles.Single(tf => tf.Content.HasPosition);
            var point = source.Content.GetPointFromPosition();

            var request = CreateRequest(source.FileName, point.Line, point.Offset, timeout: 60000, wantMetadata: wantMetadata);

            var requestHandler = GetRequestHandler(SharedOmniSharpTestHost);
            return await requestHandler.Handle(request);
        }

        protected GotoTypeDefinitionRequest CreateRequest(string fileName, int line, int column, bool wantMetadata, int timeout = 60000)
           => new GotoTypeDefinitionRequest
           {
               FileName = fileName,
               Line = line,
               Column = column,
               WantMetadata = wantMetadata,
               Timeout = timeout
           };

        protected IEnumerable<(int Line, int Column, string FileName, SourceGeneratedFileInfo SourceGeneratorInfo)> GetInfo(GotoTypeDefinitionResponse response)
        {
            if (response.Definitions is null)
                yield break;

            foreach (var definition in response.Definitions)
            {
                yield return (definition.Location.Range.Start.Line, definition.Location.Range.Start.Column, definition.Location.FileName, definition.SourceGeneratedFileInfo);
            }
        }

        protected MetadataSource GetMetadataSource(GotoTypeDefinitionResponse response)
        {
            Assert.Single(response.Definitions);
            return response.Definitions[0].MetadataSource;
        }
    }
}
