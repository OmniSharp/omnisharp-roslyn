using System.Linq;
using System.Threading.Tasks;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using OmniSharp.Roslyn.CSharp.Services.Navigation;
using OmniSharp.Models.Metadata;
using TestUtility;
using Xunit;
using Xunit.Abstractions;
using System.Collections.Generic;
using OmniSharp.Models.V2.GotoDefinition;

namespace OmniSharp.Roslyn.CSharp.Tests
{
    public class GoToDefinitionV2Facts : AbstractSingleRequestHandlerTestFixture<GotoDefinitionServiceV2>
    {
        public GoToDefinitionV2Facts(ITestOutputHelper output, SharedOmniSharpHostFixture sharedOmniSharpHostFixture)
            : base(output, sharedOmniSharpHostFixture)
        {
        }

        protected override string EndpointName => OmniSharpEndpoints.V2.GotoDefinition;

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
        public async Task ReturnsOnPropertAccessorGet(string filename)
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
        public async Task ReturnOnPropertAccessorSet(string filename)
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
        public async Task ReturnOnPropertyAccessorPropertyDef(string filename)
        {
            var testFile = new TestFile(filename, @"
class Test {
    public int {|def:Fo$$o|} { get; set; }
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
    public int {|def:Foo|} { get; set; }

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

    public int {|def:foo|};

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

    public int {|def:foo|};

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
    public int {|def:Foo|} { get; set; }

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

        [Theory]
        [InlineData("bar.cs")]
        [InlineData("bar.csx")]
        public async Task ReturnsMultiplePartialTypeDefinition(string filename)
        {
            var testFile = new TestFile(filename, @"
partial class {|def:Class|}
{
    Cla$$ss c;
}
partial class {|def:Class|}
{
}");

            await TestGoToSourceAsync(testFile);
        }

        [Fact]
        public async Task ReturnsMultiplePartialTypeDefinition_MultipleFiles()
        {
            var testFile1 = new TestFile("bar.cs", @"
partial class {|def:Class|}
{
    Cla$$ss c;
}
");

            var testFile2 = new TestFile("baz.cs", @"
partial class {|def:Class|}
{
}");

            await TestGoToSourceAsync(testFile1, testFile2);
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
                var gotoDefinitionRequest = new GotoDefinitionRequest
                {
                    FileName = testFile.FileName,
                    Line = point.Line,
                    Column = point.Offset,
                    WantMetadata = true,
                    Timeout = 60000
                };
                var gotoDefinitionRequestHandler = GetRequestHandler(host);
                var gotoDefinitionResponse = await gotoDefinitionRequestHandler.Handle(gotoDefinitionRequest);

                // 2. now, based on the response information
                // go to the metadata endpoint, and ask for "int" specific metadata
                var metadataRequest = new MetadataRequest
                {
                    AssemblyName = gotoDefinitionResponse.Definitions.Single().MetadataSource.AssemblyName,
                    TypeName = gotoDefinitionResponse.Definitions.Single().MetadataSource.TypeName,
                    ProjectName = gotoDefinitionResponse.Definitions.Single().MetadataSource.ProjectName,
                    Language = gotoDefinitionResponse.Definitions.Single().MetadataSource.Language
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
                var metadataNavigationRequest = new GotoDefinitionRequest
                {
                    FileName = metadataResponse.SourceName,
                    Line = relevantLineSpan.StartLinePosition.Line,
                    Column = relevantLineSpan.StartLinePosition.Character,
                    WantMetadata = true
                };
                var metadataNavigationResponse = await gotoDefinitionRequestHandler.Handle(metadataNavigationRequest);

                // 5. validate the response to be matching the expected IComparable meta info
                Assert.NotNull(metadataNavigationResponse.Definitions.Single().MetadataSource);
                Assert.Equal(AssemblyHelpers.CorLibName, metadataNavigationResponse.Definitions.Single().MetadataSource.AssemblyName);
                Assert.Equal("System.IComparable", metadataNavigationResponse.Definitions.Single().MetadataSource.TypeName);

                Assert.NotEqual(0, metadataNavigationResponse.Definitions.Single().Location.Range.Start.Line);
                Assert.NotEqual(0, metadataNavigationResponse.Definitions.Single().Location.Range.Start.Column);
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
            var gotoDefinitionRequest = new GotoDefinitionRequest
            {
                FileName = testFile.FileName,
                Line = point.Line,
                Column = point.Offset,
                WantMetadata = true,
                Timeout = 60000
            };
            var gotoDefinitionRequestHandler = GetRequestHandler(host);
            var gotoDefinitionResponse = await gotoDefinitionRequestHandler.Handle(gotoDefinitionRequest);

            // 2. now, based on the response information
            // go to the metadata endpoint, and ask for "int" specific decompiled source
            var metadataRequest = new MetadataRequest
            {
                AssemblyName = gotoDefinitionResponse.Definitions.Single().MetadataSource.AssemblyName,
                TypeName = gotoDefinitionResponse.Definitions.Single().MetadataSource.TypeName,
                ProjectName = gotoDefinitionResponse.Definitions.Single().MetadataSource.ProjectName,
                Language = gotoDefinitionResponse.Definitions.Single().MetadataSource.Language,
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
            Assert.Equal("// Decompiled with ICSharpCode.Decompiler 7.0.0.6488", comments[1].ToString());

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
            var metadataNavigationRequest = new GotoDefinitionRequest
            {
                FileName = metadataResponse.SourceName,
                Line = relevantLineSpan.StartLinePosition.Line,
                Column = relevantLineSpan.StartLinePosition.Character,
                WantMetadata = true
            };
            var metadataNavigationResponse = await gotoDefinitionRequestHandler.Handle(metadataNavigationRequest);

            // 5. validate the response to be matching the expected IComparable meta info
            Assert.NotNull(metadataNavigationResponse.Definitions.Single().MetadataSource);
            Assert.Equal(AssemblyHelpers.CorLibName, metadataNavigationResponse.Definitions.Single().MetadataSource.AssemblyName);
            Assert.Equal("System.IComparable", metadataNavigationResponse.Definitions.Single().MetadataSource.TypeName);

            Assert.NotEqual(0, metadataNavigationResponse.Definitions.Single().Location.Range.Start.Line);
            Assert.NotEqual(0, metadataNavigationResponse.Definitions.Single().Location.Range.Start.Column);
        }

        [Fact]
        public async Task ReturnsNoResultsButDoesNotThrowForNamespaces()
        {
            var testFile = new TestFile("foo.cs", "namespace F$$oo {}");
            var response = await GetResponseAsync(new[] { testFile }, wantMetadata: false);
            Assert.Null(response.Definitions);
        }

        private async Task TestGoToSourceAsync(params TestFile[] testFiles)
        {
            var response = await GetResponseAsync(testFiles, wantMetadata: false);

            var targets =
                from tf in testFiles
                from span in tf.Content.GetSpans("def")
                select (tf, span);

            if (targets.Any())
            {
                foreach (var (file, definitionSpan) in targets)
                {
                    var definitionRange = file.Content.GetRangeFromSpan(definitionSpan);

                    Assert.Contains(response.Definitions,
                        def => file.FileName == def.Location.FileName
                               && definitionRange.Start.Line == def.Location.Range.Start.Line
                               && definitionRange.Start.Offset == def.Location.Range.Start.Column);
                }
            }
            else
            {
                Assert.Null(response.Definitions);
            }
        }

        private async Task TestDecompilationAsync(TestFile testFile, string expectedAssemblyName, string expectedTypeName)
        {
            using var host = CreateOmniSharpHost(new[] { testFile }, new Dictionary<string, string>
            {
                ["RoslynExtensionsOptions:EnableDecompilationSupport"] = "true"
            });

            var response = await GetResponseAsync(new[] { testFile }, wantMetadata: true);

            Assert.NotNull(response.Definitions.Single().MetadataSource);
            Assert.Equal(expectedAssemblyName, response.Definitions.Single().MetadataSource.AssemblyName);
            Assert.Equal(expectedTypeName, response.Definitions.Single().MetadataSource.TypeName);
        }

        private async Task TestGoToMetadataAsync(TestFile testFile, string expectedAssemblyName, string expectedTypeName)
        {
            var response = await GetResponseAsync(new[] { testFile }, wantMetadata: true);

            Assert.NotNull(response.Definitions.Single().MetadataSource);
            Assert.Equal(expectedAssemblyName, response.Definitions.Single().MetadataSource.AssemblyName);
            Assert.Equal(expectedTypeName, response.Definitions.Single().MetadataSource.TypeName);

            // We probably shouldn't hard code metadata locations (they could change randomly)
            Assert.NotEqual(0, response.Definitions.Single().Location.Range.Start.Line);
            Assert.NotEqual(0, response.Definitions.Single().Location.Range.Start.Column);
        }

        private async Task<GotoDefinitionResponse> GetResponseAsync(TestFile[] testFiles, bool wantMetadata)
        {
            SharedOmniSharpTestHost.AddFilesToWorkspace(testFiles);
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

            var requestHandler = GetRequestHandler(SharedOmniSharpTestHost);
            return await requestHandler.Handle(request);
        }
    }
}
