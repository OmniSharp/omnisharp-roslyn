using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Diagnostics;
using OmniSharp.Mef;
using OmniSharp.Models.Metadata;
using OmniSharp.Models.v1.SourceGeneratedFile;
using OmniSharp.Roslyn.CSharp.Services.Navigation;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using System.Threading.Tasks;
using TestUtility;
using Xunit;
using Xunit.Abstractions;

namespace OmniSharp.Roslyn.CSharp.Tests
{
    public abstract class AbstractGoToDefinitionFacts<TGotoDefinitionService, TGotoDefinitionRequest, TGotoDefinitionResponse> : AbstractSingleRequestHandlerTestFixture<TGotoDefinitionService>
        where TGotoDefinitionService : IRequestHandler<TGotoDefinitionRequest, TGotoDefinitionResponse>
    {
        protected AbstractGoToDefinitionFacts(ITestOutputHelper testOutput, SharedOmniSharpHostFixture sharedOmniSharpHostFixture) : base(testOutput, sharedOmniSharpHostFixture)
        {
        }


        [Theory]
        [InlineData("foo.cs")]
        [InlineData("foo.csx")]
        public async Task ReturnsDefinitionInSameFile(string filename)
        {
            var testFile = new TestFile(filename, @"
class {|def:Foo|} {
    private F$$oo foo;
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
        public async Task DoesNotReturnOnPropertyAccessorPropertyDef(string filename)
        {
            var testFile = new TestFile(filename, @"
class Test {
    public int |def:Foo| Fo$$o{ get; set; }
}");

            await TestGoToSourceAsync(testFile);
        }

        [Theory]
        [InlineData("foo.cs")]
        [InlineData("foo.csx")]
        public async Task ReturnsOnPropertyAccessorPropertySetting(string filename)
        {
            var testFile = new TestFile(filename, @"
class Test {
    public int |def:Foo|{ get; set; }

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
        public async Task ReturnsOnPropertyAccessorField1(string filename)
        {
            var testFile = new TestFile(filename, @"
class Test {

    public int |def:foo|;

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

    public int |def:foo|;

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
        public async Task ReturnsOnPropertyAccessorPropertyGetting(string filename)
        {
            var testFile = new TestFile(filename, @"
class Test {
    public int |def:Foo|{ get; set; }

    public static void main()
    {
        Foo = 3;
    Console.WriteLine(F$$oo);
    }
}");

            await TestGoToSourceAsync(testFile);
        }



        [Theory]
        [InlineData("bar.cs")]
        [InlineData("bar.csx")]
        public async Task ReturnsPartialMethodDefinitionWithBody(string filename)
        {
            var testFile = new TestFile(filename, @"
    public partial class MyClass
    {
        public MyClass()
        {
            Met$$hod();
        }

        partial void {|def:Method|}()
        {
            //do stuff
        }
    }

    public partial class MyClass
    {
        partial void Method();
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

        [Theory]
        [InlineData("bar.cs")]
        [InlineData("bar.csx")]
        public async Task ReturnsDefinitionInMetadata_WhenSymbolIsStaticMethod(string filename)
        {
            var testFile = new TestFile(filename, @"
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

        [Theory]
        [InlineData("bar.cs")]
        [InlineData("bar.csx")]
        public async Task ReturnsDecompiledDefinition_WhenSymbolIsStaticMethod(string filename)
        {
            var testFile = new TestFile(filename, @"
using System;
class Bar {
    public void Baz() {
        Guid.NewG$$uid();
    }
}");

            await TestDecompilationAsync(testFile,
                expectedAssemblyName: AssemblyHelpers.CorLibName,
                expectedTypeName: "System.Guid");
        }

        [Theory]
        [InlineData("bar.cs")]
        [InlineData("bar.csx")]
        public async Task ReturnsDefinitionInMetadata_WhenSymbolIsInstanceMethod(string filename)
        {
            var testFile = new TestFile(filename, @"
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

        [Theory]
        [InlineData("bar.cs")]
        [InlineData("bar.csx")]
        public async Task ReturnsDecompiledDefinition_WhenSymbolIsInstanceMethod(string filename)
        {
            var testFile = new TestFile(filename, @"
using System.Collections.Generic;
class Bar {
    public void Baz() {
        var foo = new List<string>();
        foo.ToAr$$ray();
    }
}");

            await TestDecompilationAsync(testFile,
                expectedAssemblyName: AssemblyHelpers.CorLibName,
                expectedTypeName: "System.Collections.Generic.List`1");
        }

        [Theory]
        [InlineData("bar.cs")]
        [InlineData("bar.csx")]
        public async Task ReturnsDefinitionInMetadata_WhenSymbolIsGenericType(string filename)
        {
            var testFile = new TestFile(filename, @"
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

        [Theory]
        [InlineData("bar.cs")]
        [InlineData("bar.csx")]
        public async Task ReturnsDecompiledDefinition_WhenSymbolIsGenericType(string filename)
        {
            var testFile = new TestFile(filename, @"
using System.Collections.Generic;
class Bar {
    public void Baz() {
        var foo = new Li$$st<string>();
        foo.ToArray();
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
        var str = Stri$$ng.Empty;
    }
}");

            await TestGoToMetadataAsync(testFile,
                expectedAssemblyName: AssemblyHelpers.CorLibName,
                expectedTypeName: "System.String");
        }

        [Theory]
        [InlineData("bar.cs")]
        [InlineData("bar.csx")]
        public async Task ReturnsDecompiledDefinition_WhenSymbolIsType(string filename)
        {
            var testFile = new TestFile(filename, @"
using System;
class Bar {
    public void Baz() {
        var str = Stri$$ng.Empty;
    }
}");

            await TestDecompilationAsync(testFile,
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
        var number = in$$t.MaxValue;
    }
}");

            using (var host = CreateOmniSharpHost(testFile))
            {
                var point = testFile.Content.GetPointFromPosition();

                // 1. start by asking for definition of "int"
                var gotoDefinitionRequest = CreateRequest(testFile.FileName, point.Line, point.Offset, wantMetadata: true, timeout: 60000);
                var gotoDefinitionRequestHandler = GetRequestHandler(host);
                var gotoDefinitionResponse = await gotoDefinitionRequestHandler.Handle(gotoDefinitionRequest);
                var gotoDefinitionResponseMetadataSource = GetMetadataSource(gotoDefinitionResponse);

                // 2. now, based on the response information
                // go to the metadata endpoint, and ask for "int" specific metadata
                var metadataRequest = new MetadataRequest
                {
                    AssemblyName = gotoDefinitionResponseMetadataSource.AssemblyName,
                    TypeName = gotoDefinitionResponseMetadataSource.TypeName,
                    ProjectName = gotoDefinitionResponseMetadataSource.ProjectName,
                    Language = gotoDefinitionResponseMetadataSource.Language
                };
                var metadataRequestHandler = host.GetRequestHandler<MetadataService>(OmniSharpEndpoints.Metadata);
                var metadataResponse = await metadataRequestHandler.Handle(metadataRequest);

                // 3. the metadata response contains SourceName (metadata "file") and SourceText (syntax tree)
                // use the source to locate "IComparable" which is an interface implemented by Int32 struct
                var metadataTree = CSharpSyntaxTree.ParseText(metadataResponse.Source);

                var iComparable = metadataTree.GetCompilationUnitRoot().
                    DescendantNodesAndSelf().
                    OfType<BaseTypeDeclarationSyntax>().First().
                    BaseList.Types.FirstOrDefault(x => x.Type.ToString() == "IComparable");
                var relevantLineSpan = iComparable.GetLocation().GetLineSpan();

                // 4. now ask for the definition of "IComparable"
                // pass in the SourceName (metadata "file") as FileName - since it's not a regular file in our workspace
                var metadataNavigationRequest = CreateRequest(metadataResponse.SourceName, relevantLineSpan.StartLinePosition.Line, relevantLineSpan.StartLinePosition.Character, wantMetadata: true);
                var metadataNavigationResponse = await gotoDefinitionRequestHandler.Handle(metadataNavigationRequest);
                var metadataNavigationResponseMetadataSource = GetMetadataSource(metadataNavigationResponse);
                var info = GetInfo(metadataNavigationResponse);

                // 5. validate the response to be matching the expected IComparable meta info
                Assert.NotNull(metadataNavigationResponseMetadataSource);
                Assert.Equal(AssemblyHelpers.CorLibName, metadataNavigationResponseMetadataSource.AssemblyName);
                Assert.Equal("System.IComparable", metadataNavigationResponseMetadataSource.TypeName);

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
        var number = in$$t.MaxValue;
    }
}");

            using var host = CreateOmniSharpHost(new[] { testFile }, new Dictionary<string, string>
            {
                ["RoslynExtensionsOptions:EnableDecompilationSupport"] = "true"
            });

            var point = testFile.Content.GetPointFromPosition();

            // 1. start by asking for definition of "int"
            var gotoDefinitionRequest = CreateRequest(testFile.FileName, point.Line, point.Offset, wantMetadata: true, timeout: 60000);
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
                Timeout = 60000
            };
            var metadataRequestHandler = host.GetRequestHandler<MetadataService>(OmniSharpEndpoints.Metadata);
            var metadataResponse = await metadataRequestHandler.Handle(metadataRequest);

            // 3. the response contains SourceName ("file") and SourceText (syntax tree)
            // use the source to locate "IComparable" which is an interface implemented by Int32 struct
            var decompiledTree = CSharpSyntaxTree.ParseText(metadataResponse.Source);
            var compilationUnit = decompiledTree.GetCompilationUnitRoot();

            // second comment should indicate we have decompiled
            var comments = compilationUnit.DescendantTrivia().Where(t => t.Kind() == SyntaxKind.SingleLineCommentTrivia).ToArray();
            Assert.NotNull(comments);
            Assert.Equal("// Decompiled with ICSharpCode.Decompiler 7.1.0.6543", comments[1].ToString());

            // contrary to regular metadata, we should have methods with full bodies
            // this condition would fail if decompilation wouldn't work
            var methods = compilationUnit.
                DescendantNodesAndSelf().
                OfType<MethodDeclarationSyntax>().
                Where(m => m.Body != null);

            Assert.NotEmpty(methods);

            var iComparable = compilationUnit.
                DescendantNodesAndSelf().
                OfType<BaseTypeDeclarationSyntax>().First().
                BaseList.Types.FirstOrDefault(x => x.Type.ToString() == "IComparable");
            var relevantLineSpan = iComparable.GetLocation().GetLineSpan();

            // 4. now ask for the definition of "IComparable"
            // pass in the SourceName (metadata "file") as FileName - since it's not a regular file in our workspace
            var metadataNavigationRequest = CreateRequest(metadataResponse.SourceName, relevantLineSpan.StartLinePosition.Line, relevantLineSpan.StartLinePosition.Character, wantMetadata: true);
            var metadataNavigationResponse = await gotoDefinitionRequestHandler.Handle(metadataNavigationRequest);
            var metadataSourceResponse = GetMetadataSource(metadataNavigationResponse);
            var metadataNavigationInfo = GetInfo(metadataNavigationResponse);

            // 5. validate the response to be matching the expected IComparable meta info
            Assert.NotNull(metadataSource);
            Assert.Equal(AssemblyHelpers.CorLibName, metadataSourceResponse.AssemblyName);
            Assert.Equal("System.IComparable", metadataSourceResponse.TypeName);

            Assert.NotEqual(0, metadataNavigationInfo.Single().Line);
            Assert.NotEqual(0, metadataNavigationInfo.Single().Column);
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
    public void M(Generated g)
    {
        _ = g.P$$roperty;
    }
}
");

            TestHelpers.AddProjectToWorkspace(SharedOmniSharpTestHost.Workspace,
                "project.csproj",
                new[] { "netcoreapp3.1" },
                new[] { testFile },
                analyzerRefs: ImmutableArray.Create<AnalyzerReference>(new TestGeneratorReference(
                    context => context.AddSource("GeneratedFile", generatedTestFile.Content.Code))));

            var point = testFile.Content.GetPointFromPosition();

            var gotoDefRequest = CreateRequest(FileName, point.Line, point.Offset, wantMetadata: true);
            var gotoDefHandler = GetRequestHandler(SharedOmniSharpTestHost);
            var response = await gotoDefHandler.Handle(gotoDefRequest);
            var info = GetInfo(response).Single();

            Assert.NotNull(info.SourceGeneratorInfo);

            var expectedSpan = generatedTestFile.Content.GetSpans("propertyName").Single();
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

        protected async Task<TGotoDefinitionResponse> GetResponseAsync(TestFile[] testFiles, bool wantMetadata)
        {
            SharedOmniSharpTestHost.AddFilesToWorkspace(testFiles);
            var source = testFiles.Single(tf => tf.Content.HasPosition);
            var point = source.Content.GetPointFromPosition();

            var request = CreateRequest(source.FileName, point.Line, point.Offset, timeout: 60000, wantMetadata: wantMetadata);

            var requestHandler = GetRequestHandler(SharedOmniSharpTestHost);
            return await requestHandler.Handle(request);
        }

        protected abstract TGotoDefinitionRequest CreateRequest(string fileName, int line, int column, bool wantMetadata, int timeout = 60000);
        protected abstract MetadataSource GetMetadataSource(TGotoDefinitionResponse response);
        protected abstract IEnumerable<(int Line, int Column, string FileName, SourceGeneratedFileInfo SourceGeneratorInfo)> GetInfo(TGotoDefinitionResponse response);
    }
}
