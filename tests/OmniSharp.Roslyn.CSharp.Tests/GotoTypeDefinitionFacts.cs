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
    public abstract class GotoTypeDefinitionTestBase : AbstractSingleRequestHandlerTestFixture<GotoTypeDefinitionService>
    {
        protected GotoTypeDefinitionTestBase(ITestOutputHelper output, SharedOmniSharpHostFixture sharedOmniSharpHostFixture)
            : base(output, sharedOmniSharpHostFixture)
        {
        }

        protected override string EndpointName => OmniSharpEndpoints.GotoTypeDefinition;

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

    public class GotoTypeDefinitionFacts : GotoTypeDefinitionTestBase
    {
        public GotoTypeDefinitionFacts(ITestOutputHelper output, SharedOmniSharpHostFixture sharedOmniSharpHostFixture)
            : base(output, sharedOmniSharpHostFixture)
        {
        }

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
                new[] { "net8.0" },
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
    }
}
