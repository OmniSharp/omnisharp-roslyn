using System.Collections.Generic;
using System.Collections.Immutable;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Diagnostics;
using OmniSharp.Models;
using OmniSharp.Models.FindUsages;
using OmniSharp.Models.v1.SourceGeneratedFile;
using OmniSharp.Roslyn.CSharp.Services.Navigation;
using Roslyn.Test.Utilities;
using TestUtility;
using Xunit;
using Xunit.Abstractions;

namespace OmniSharp.Roslyn.CSharp.Tests
{
    public class FindReferencesFacts : AbstractSingleRequestHandlerTestFixture<FindUsagesService>
    {
        public FindReferencesFacts(ITestOutputHelper output, SharedOmniSharpHostFixture sharedOmniSharpHostFixture)
            : base(output, sharedOmniSharpHostFixture)
        {
        }

        protected override string EndpointName => OmniSharpEndpoints.FindUsages;

        [Fact]
        public async Task CanFindReferencesOfLocalVariable()
        {
            const string code = @"
                public class Foo
                {
                    public Foo(string s)
                    {
                        var pr$$op = s + 'abc';

                        prop += 'woo';
                    }
                }";

            var usages = await FindUsagesAsync(code);
            Assert.Equal(2, usages.QuickFixes.Count());
        }

        [Fact]
        public async Task CanFindReferencesOfMethodParameter()
        {
            const string code = @"
                public class Foo
                {
                    public Foo(string $$s)
                    {
                        var prop = s + 'abc';
                    }
                }";

            var usages = await FindUsagesAsync(code);
            Assert.Equal(2, usages.QuickFixes.Count());
        }

        [Fact]
        public async Task CanFindReferencesOfField()
        {
            const string code = @"
                public class Foo
                {
                    public string p$$rop;
                }

                public class FooConsumer
                {
                    public FooConsumer()
                    {
                        var foo = new Foo();
                        var prop = foo.prop;
                    }
                }";

            var usages = await FindUsagesAsync(code);
            Assert.Equal(2, usages.QuickFixes.Count());
        }

        [Fact]
        public async Task CanFindReferencesOfConstructor()
        {
            const string code = @"
                public class Foo
                {
                    public F$$oo() {}
                }

                public class FooConsumer
                {
                    public FooConsumer()
                    {
                        var temp = new Foo();
                    }
                }";

            var usages = await FindUsagesAsync(code);
            Assert.Equal(2, usages.QuickFixes.Count());
        }

        [Fact]
        public async Task CanFindReferencesOfMethod()
        {
            const string code = @"
                public class Foo
                {
                    public void b$$ar() { }
                }

                public class FooConsumer
                {
                    public FooConsumer()
                    {
                        new Foo().bar();
                    }
                }";

            var usages = await FindUsagesAsync(code);
            Assert.Equal(2, usages.QuickFixes.Count());
        }

        [Theory]
        [InlineData(9, "public FooConsumer()")]
        [InlineData(100, "new Foo().bar();")]
        public async Task CanFindReferencesWithLineMapping(int mappingLine, string expectedMappingText)
        {
            var code = @"
                public class Foo
                {
                    public void b$$ar() { }
                }

                public class FooConsumer
                {
                    public FooConsumer()
                    {
#line " + mappingLine + @"
                        new Foo().bar();
#line default
                    }
                }";

            var usages = await FindUsagesAsync(code);
            Assert.Equal(2, usages.QuickFixes.Count());

            var quickFixes = usages.QuickFixes.OrderBy(x => x.Line);
            var regularResult = quickFixes.ElementAt(0);
            var mappedResult = quickFixes.ElementAt(1);

            Assert.EndsWith("dummy.cs", regularResult.FileName);
            Assert.EndsWith("dummy.cs", mappedResult.FileName);

            Assert.Equal("public void bar() { }", regularResult.Text);
            Assert.Equal(expectedMappingText, mappedResult.Text);

            Assert.Equal(3, regularResult.Line);
            Assert.Equal(mappingLine - 1, mappedResult.Line);

            // regular result has regular postition
            Assert.Equal(32, regularResult.Column);
            Assert.Equal(35, regularResult.EndColumn);

            // mapped result has column 0,0
            Assert.Equal(34, mappedResult.Column);
            Assert.Equal(37, mappedResult.EndColumn);
        }

        [Theory]
        [InlineData(1, "// hello", true)] // everything correct
        [InlineData(100, "new Foo().bar();", true)] // file exists in workspace but mapping incorrect
        [InlineData(1, "new Foo().bar();", false)] // file doesn't exist in workspace but mapping correct
        public async Task CanFindReferencesWithLineMappingAcrossFiles(int mappingLine, string expectedMappingText, bool mappedFileExistsInWorkspace)
        {
            var testFiles = new List<TestFile>()
            {
                new TestFile("a.cs", @"
                public class Foo
                {
                    public void b$$ar() { }
                }

                public class FooConsumer
                {
                    public FooConsumer()
                    {
#line "+mappingLine+@" ""b.cs""
                        new Foo().bar();
#line default
                    }
                }"),

            };

            if (mappedFileExistsInWorkspace)
            {
                testFiles.Add(new TestFile("b.cs",
                    @"// hello"));
            }

            var usages = await FindUsagesAsync(testFiles.ToArray(), onlyThisFile: false);
            Assert.Equal(2, usages.QuickFixes.Count());

            var regularResult = usages.QuickFixes.ElementAt(0);
            var mappedResult = usages.QuickFixes.ElementAt(1);

            Assert.EndsWith("a.cs", regularResult.FileName);
            Assert.EndsWith("b.cs", mappedResult.FileName);

            Assert.Equal(3, regularResult.Line);
            Assert.Equal(mappingLine - 1, mappedResult.Line);

            Assert.Equal("public void bar() { }", regularResult.Text);
            Assert.Equal(expectedMappingText, mappedResult.Text);

            // regular result has regular postition
            Assert.Equal(32, regularResult.Column);
            Assert.Equal(35, regularResult.EndColumn);

            // mapped result has column 0,0
            Assert.Equal(0, mappedResult.Column);
            Assert.Equal(0, mappedResult.EndColumn);
        }

        [Fact]
        public async Task CanFindReferencesWithNegativeLineMapping()
        {
            var testFiles = new List<TestFile>()
            {
                new TestFile("a.cs", @"
                public class Foo
                {
                    public void b$$ar() { }
                }

                public class FooConsumer
                {
                    public FooConsumer()
                    {
#line -10 ""b.cs""
                        new Foo().bar();
#line default
                    }
                }"),

            };

            testFiles.Add(new TestFile("b.cs",
                    @"// hello"));

            var usages = await FindUsagesAsync(testFiles.ToArray(), onlyThisFile: false);
            Assert.Equal(2, usages.QuickFixes.Count());

            var regularResult = usages.QuickFixes.ElementAt(0);
            var mappedResult = usages.QuickFixes.ElementAt(1);

            Assert.EndsWith("a.cs", regularResult.FileName);
            Assert.EndsWith("a.cs", mappedResult.FileName);

            Assert.Equal(3, regularResult.Line);
            Assert.Equal(11, mappedResult.Line);

            Assert.Equal("public void bar() { }", regularResult.Text);
            Assert.Equal("new Foo().bar();", mappedResult.Text);

            Assert.Equal(32, regularResult.Column);
            Assert.Equal(35, regularResult.EndColumn);
            Assert.Equal(34, mappedResult.Column);
            Assert.Equal(37, mappedResult.EndColumn);
        }

        [Fact]
        public async Task ExcludesMethodDefinition()
        {
            const string code = @"
                public class Foo
                {
                    public void b$$ar() { }
                }

                public class FooConsumer
                {
                    public FooConsumer()
                    {
                        new Foo().bar();
                    }
                }";

            var usages = await FindUsagesAsync(code, excludeDefinition: true);
            Assert.Single(usages.QuickFixes);
        }

        [Fact]
        public async Task CanFindReferencesOfPublicAutoProperty()
        {
            const string code = @"
                public class Foo
                {
                    public string p$$rop {get;set;}
                }

                public class FooConsumer
                {
                    public FooConsumer()
                    {
                        var foo = new Foo();
                        var prop = foo.prop;
                    }
                }";

            var usages = await FindUsagesAsync(code);
            Assert.Equal(2, usages.QuickFixes.Count());
        }

        [Fact]
        public async Task CanFindReferencesOfPublicIndexerProperty()
        {
            const string code = @"
                public class Foo
                {
                    int prop;

                    public int th$$is[int index]
                    {
                        get { return prop; }
                        set { prop = value; }
                    }
                }

                public class FooConsumer
                {
                    public FooConsumer()
                    {
                        var foo = new Foo();
                        var prop = foo[0];
                        foo[0] = prop;
                    }
                }";

            var usages = await FindUsagesAsync(code);
            Assert.Equal(3, usages.QuickFixes.Count());
        }

        [Fact]
        public async Task CanFindReferencesOfClass()
        {
            const string code = @"
                public class F$$oo
                {
                    public string prop {get;set;}
                }

                public class FooConsumer
                {
                    public FooConsumer()
                    {
                        var temp = new Foo();
                        var prop = foo.prop;
                    }
                }";

            var usages = await FindUsagesAsync(code);
            Assert.Equal(2, usages.QuickFixes.Count());
        }

        [Fact]
        public async Task CanFindReferencesOfOperatorOverloads()
        {
            const string code = @"
                public struct Vector2
                {
                    public float x;
                    public float y;

                    public static Vector2 operator $$+(Vector2 lhs, Vector2 rhs) => new Vector2()
                    {
                        x = lhs.x + rhs.x,
                        y = lhs.y + rhs.y,
                    };
                }

                public class Vector2Consumer
                {
                    public Vector2Consumer()
                    {
                        var a = new Vector2();
                        var b = new Vector2();
                        var c = a + b;
                    }
                }";

            var usages = await FindUsagesAsync(code);
            Assert.Equal(2, usages.QuickFixes.Count());
        }

        [Fact]
        public async Task LimitReferenceSearchToThisFile()
        {
            var testFiles = new[]
            {
                new TestFile("a.cs", @"
                    public class F$$oo {
                        public Foo Clone() {
                            return null;
                        }
                    }"),
                new TestFile("b.cs",
                    @"public class Bar : Foo { }")
            };

            var usages = await FindUsagesAsync(testFiles, onlyThisFile: false);
            Assert.Equal(3, usages.QuickFixes.Count());
            Assert.EndsWith("a.cs", usages.QuickFixes.ElementAt(0).FileName);
            Assert.EndsWith("a.cs", usages.QuickFixes.ElementAt(1).FileName);
            Assert.EndsWith("b.cs", usages.QuickFixes.ElementAt(2).FileName);

            usages = await FindUsagesAsync(testFiles, onlyThisFile: true);
            Assert.Equal(2, usages.QuickFixes.Count());
            Assert.EndsWith("a.cs", usages.QuickFixes.ElementAt(0).FileName);
            Assert.EndsWith("a.cs", usages.QuickFixes.ElementAt(1).FileName);
        }

        [Fact]
        public async Task MappedLocationFileNameProperlyRootedInAdditionalDocuments()
        {
            var folderPath = Directory.GetCurrentDirectory();
            var relativeFile = ".\\Index.cshtml";
            var mappedFilePath = Path.GetFullPath(Path.Combine(folderPath, relativeFile));

            var testFiles = new[]
            {
                new TestFile("Constants.cs", @"
                    public static class Constants
                    {
                        public const string My$$Text = ""Hello World"";
                    }"),
                new TestFile("Index.cshtml.cs", @"
                    using Microsoft.AspNetCore.Mvc.RazorPages;

                    public class IndexModel : PageModel
                    {
                        public IndexModel()
                        {
                        }

                        public void OnGet()
                        {

                        }
                    }"),
                new TestFile("Index.cshtml_virtual.cs", $@"
                    #line 1 ""{relativeFile}""
                    Constants.MyText
                    #line default
                    #line hidden"),
                new TestFile(mappedFilePath, "<p>@Constants.MyText</p>")
            };

            var usages = await FindUsagesAsync(testFiles, onlyThisFile: false, folderPath: folderPath);

            Assert.DoesNotContain(usages.QuickFixes, location => location.FileName.EndsWith("Index.cshtml_virtual.cs"));
            Assert.DoesNotContain(usages.QuickFixes, location => location.FileName.Equals(relativeFile));

            var quickFix = Assert.Single(usages.QuickFixes, location => location.FileName.Equals(mappedFilePath));
            Assert.Empty(quickFix.Projects);
        }

        [Fact]
        public async Task MappedLocationFileNameProperlyRootedInMiscellaneousWorkspace()
        {
            var folderPath = Directory.GetCurrentDirectory();
            var relativeFile = ".\\Index.cshtml.cs";
            var mappedFilePath = Path.GetFullPath(Path.Combine(folderPath, relativeFile));

            var testFiles = new[]
            {
                new TestFile("Constants.cs", @"
                    public static class Constants
                    {
                        public const string My$$Text = ""Hello World"";
                    }"),
                new TestFile("Index.cshtml_virtual.cs", $@"
                    #line 1 ""{relativeFile}""
                    Constants.MyText
                    #line default
                    #line hidden")
            };

            var miscFile = new TestFile(mappedFilePath, "// Constants.MyText;");

            SharedOmniSharpTestHost.AddFilesToWorkspace(folderPath, testFiles);
            SharedOmniSharpTestHost.Workspace.TryAddMiscellaneousDocument(
                miscFile.FileName,
                TextLoader.From(TextAndVersion.Create(miscFile.Content.Text, VersionStamp.Create())),
                LanguageNames.CSharp);

            var testFile = testFiles.Single(tf => tf.Content.HasPosition);
            var usages = await FindUsagesAsync(testFile, onlyThisFile: false);

            Assert.DoesNotContain(usages.QuickFixes, location => location.FileName.EndsWith("Index.cshtml_virtual.cs"));
            Assert.DoesNotContain(usages.QuickFixes, location => location.FileName.Equals(relativeFile));

            var quickFix = Assert.Single(usages.QuickFixes, location => location.FileName.Equals(mappedFilePath));
            Assert.Empty(quickFix.Projects);
        }

        [Fact]
        public async Task DontFindDefinitionInAnotherFile()
        {
            var testFiles = new[]
            {
                new TestFile("a.cs",
                 @"public class Bar : F$$oo {}"),
                new TestFile("b.cs", @"
                    public class Foo {
                        public Foo Clone() {
                            return null;
                        }
                    }")
            };

            var usages = await FindUsagesAsync(testFiles, onlyThisFile: true);
            Assert.Single(usages.QuickFixes);
            Assert.EndsWith("a.cs", usages.QuickFixes.ElementAt(0).FileName);
        }

        [Theory]
        [InlineData("public Foo(string $$event)")]
        [InlineData("pu$$blic Foo(string s)")]
        public async Task DoesNotCrashOnInvalidReference(string methodDefinition)
        {
            var code = @$"
                public class Foo
                {{
                    {methodDefinition}
                    {{
                    }}
                }}";

            var exception = await Record.ExceptionAsync(async () =>
            {
                var usages = await FindUsagesAsync(code);
                Assert.NotNull(usages);
            });

            Assert.Null(exception);
        }

        [Fact]
        public async Task ReturnsGeneratedReferences()
        {
            const string Source = @"
public partial class Generated
{
    public int Property { get; set; }
}
";
            const string FileName = "real.cs";
            var testFile = new TestFile(FileName, @"
class C
{
    Generate$$d G;
}
");

            TestHelpers.AddProjectToWorkspace(SharedOmniSharpTestHost.Workspace,
                "project.csproj",
                new[] { "net6.0" },
                new[] { testFile },
                analyzerRefs: ImmutableArray.Create<AnalyzerReference>(new TestGeneratorReference(
                    context => context.AddSource("GeneratedFile", Source))));

            var response = await FindUsagesAsync(testFile, onlyThisFile: false, excludeDefinition: false);

            var result = response.QuickFixes.Cast<SymbolLocation>().Single(s => s.GeneratedFileInfo is not null);
            Assert.NotNull(result);
            AssertEx.Equal(@"OmniSharp.Roslyn.CSharp.Tests\OmniSharp.Roslyn.CSharp.Tests.TestSourceGenerator\GeneratedFile.cs", result.FileName.Replace("/", @"\"));

            var sourceGeneratedFileHandler = SharedOmniSharpTestHost.GetRequestHandler<SourceGeneratedFileService>(OmniSharpEndpoints.SourceGeneratedFile);
            var sourceGeneratedRequest = new SourceGeneratedFileRequest
            {
                DocumentGuid = result.GeneratedFileInfo.DocumentGuid,
                ProjectGuid = result.GeneratedFileInfo.ProjectGuid
            };

            var sourceGeneratedFileResponse = await sourceGeneratedFileHandler.Handle(sourceGeneratedRequest);
            Assert.NotNull(sourceGeneratedFileResponse);
            AssertEx.Equal(Source, sourceGeneratedFileResponse.Source);
            AssertEx.Equal(@"OmniSharp.Roslyn.CSharp.Tests\OmniSharp.Roslyn.CSharp.Tests.TestSourceGenerator\GeneratedFile.cs", sourceGeneratedFileResponse.SourceName.Replace("/", @"\"));
        }

        private Task<QuickFixResponse> FindUsagesAsync(string code, bool excludeDefinition = false)
        {
            return FindUsagesAsync(new[] { new TestFile("dummy.cs", code) }, false, excludeDefinition);
        }

        private Task<QuickFixResponse> FindUsagesAsync(TestFile[] testFiles, bool onlyThisFile, bool excludeDefinition = false, string folderPath = null)
        {
            SharedOmniSharpTestHost.AddFilesToWorkspace(folderPath, testFiles);
            var testFile = testFiles.Single(tf => tf.Content.HasPosition);
            return FindUsagesAsync(testFile, onlyThisFile, excludeDefinition);
        }

        private async Task<QuickFixResponse> FindUsagesAsync(TestFile testFile, bool onlyThisFile, bool excludeDefinition = false)
        {
            var point = testFile.Content.GetPointFromPosition();

            var requestHandler = GetRequestHandler(SharedOmniSharpTestHost);

            var request = new FindUsagesRequest
            {
                Line = point.Line,
                Column = point.Offset,
                FileName = testFile.FileName,
                Buffer = testFile.Content.Code,
                OnlyThisFile = onlyThisFile,
                ExcludeDefinition = excludeDefinition
            };

            return await requestHandler.Handle(request);
        }
    }
}
