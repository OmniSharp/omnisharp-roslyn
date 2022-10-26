using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using OmniSharp.Extensions.LanguageServer.Protocol;
using OmniSharp.Extensions.LanguageServer.Protocol.Document;
using OmniSharp.Extensions.LanguageServer.Protocol.Models;
using OmniSharp.Models;
using OmniSharp.Models.FindUsages;
using OmniSharp.Roslyn.CSharp.Services.Navigation;
using TestUtility;
using Xunit;
using Xunit.Abstractions;

namespace OmniSharp.Lsp.Tests
{
    public class ReferencesHandlerFacts : AbstractLanguageServerTestBase
    {
        public ReferencesHandlerFacts(ITestOutputHelper output)
            : base(output)
        {
        }

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
            Assert.Equal(2, usages.Count());
        }

        [Fact]
        public async Task Cannot_Find_References_For_Empty_Files()
        {
            var response = await Client.TextDocument.RequestReferences(new ReferenceParams()
            {
                Position = (0, 0),
                TextDocument = "notfound.cs"
            }, CancellationToken);

            Assert.Empty(response);
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
            Assert.Equal(2, usages.Count());
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
            Assert.Equal(2, usages.Count());
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
            Assert.Equal(2, usages.Count());
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
            Assert.Equal(2, usages.Count());
        }

        [Theory]
        [InlineData(9)]
        [InlineData(100)]
        public async Task CanFindReferencesWithLineMapping(int mappingLine)
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
            Assert.Equal(2, usages.Count());

            var quickFixes = usages.OrderBy(x => x.Range.Start.Line);
            var regularResult = quickFixes.ElementAt(0);
            var mappedResult = quickFixes.ElementAt(1);

            Assert.Equal("dummy.cs", regularResult.Uri);
            Assert.Equal("dummy.cs", mappedResult.Uri);

            // Assert.Equal("public void bar() { }", regularResult.Text);
            // Assert.Equal(expectedMappingText, mappedResult.Text);

            Assert.Equal(3, regularResult.Range.Start.Line);
            Assert.Equal(mappingLine - 1, mappedResult.Range.Start.Line);

            // regular result has regular postition
            Assert.Equal(32, regularResult.Range.Start.Character);
            Assert.Equal(35, regularResult.Range.End.Character);

            // mapped result has column 0,0
            Assert.Equal(34, mappedResult.Range.Start.Character);
            Assert.Equal(37, mappedResult.Range.End.Character);
        }

        [Theory]
        [InlineData(1, true)] // everything correct
        [InlineData(100, true)] // file exists in workspace but mapping incorrect
        [InlineData(1, false)] // file doesn't exist in workspace but mapping correct
        public async Task CanFindReferencesWithLineMappingAcrossFiles(int mappingLine,
            bool mappedFileExistsInWorkspace)
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
#line " + mappingLine + @" ""b.cs""
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

            var usages = await FindUsagesAsync(testFiles.ToArray());
            Assert.Equal(2, usages.Count());

            var regularResult = usages.ElementAt(0);
            var mappedResult = usages.ElementAt(1);

            Assert.EndsWith("a.cs", regularResult.Uri.Path);
            Assert.EndsWith("b.cs", mappedResult.Uri.Path);

            Assert.Equal(3, regularResult.Range.Start.Line);
            Assert.Equal(mappingLine - 1, mappedResult.Range.Start.Line);

            // Assert.Equal("public void bar() { }", regularResult.Text);
            // Assert.Equal(expectedMappingText, mappedResult.Text);

            // regular result has regular postition
            Assert.Equal(32, regularResult.Range.Start.Character);
            Assert.Equal(35, regularResult.Range.End.Character);

            // mapped result has column 0,0
            Assert.Equal(0, mappedResult.Range.Start.Character);
            Assert.Equal(0, mappedResult.Range.End.Character);
        }

        [Theory]
        [InlineData(9)]
        [InlineData(100)]
        public async Task FindReferencesWithLineMappingReturnsRegularPosition_Razor(int mappingLine)
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

            var usages = await FindUsagesAsync(new[] { new TestFile("dummy.razor__virtual.cs", code) }, excludeDefinition: false);
            Assert.Equal(2, usages.Count());

            var quickFixes = usages.OrderBy(x => x.Range.Start.Line);
            var regularResult = quickFixes.ElementAt(0);
            var mappedResult = quickFixes.ElementAt(1);

            Assert.Equal("dummy.razor__virtual.cs", regularResult.Uri);
            Assert.Equal("dummy.razor__virtual.cs", mappedResult.Uri);

            Assert.Equal(3, regularResult.Range.Start.Line);
            Assert.Equal(10, mappedResult.Range.Start.Line);

            // regular + mapped results have regular postition
            Assert.Equal(32, regularResult.Range.Start.Character);
            Assert.Equal(35, regularResult.Range.End.Character);

            Assert.Equal(34, mappedResult.Range.Start.Character);
            Assert.Equal(37, mappedResult.Range.End.Character);
        }

        [Theory]
        [InlineData(1, true)] // everything correct
        [InlineData(100, true)] // file exists in workspace but mapping incorrect
        [InlineData(1, false)] // file doesn't exist in workspace but mapping correct
        public async Task FindReferencesWithLineMappingAcrossFilesReturnsRegularPosition_Razor(int mappingLine,
            bool mappedFileExistsInWorkspace)
        {
            var testFiles = new List<TestFile>()
            {
                new TestFile("a.cshtml__virtual.cs", @"
                public class Foo
                {
                    public void b$$ar() { }
                }
                public class FooConsumer
                {
                    public FooConsumer()
                    {
#line " + mappingLine + @" ""b.cs""
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

            var usages = await FindUsagesAsync(testFiles.ToArray());
            Assert.Equal(2, usages.Count());

            var regularResult = usages.ElementAt(0);
            var mappedResult = usages.ElementAt(1);

            Assert.EndsWith("a.cshtml__virtual.cs", regularResult.Uri.Path);
            Assert.EndsWith("a.cshtml__virtual.cs", mappedResult.Uri.Path);

            Assert.Equal(3, regularResult.Range.Start.Line);
            Assert.Equal(10, mappedResult.Range.Start.Line);

            // regular + mapped results have regular postition
            Assert.Equal(32, regularResult.Range.Start.Character);
            Assert.Equal(35, regularResult.Range.End.Character);

            Assert.Equal(34, mappedResult.Range.Start.Character);
            Assert.Equal(37, mappedResult.Range.End.Character);
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

            var usages = await FindUsagesAsync(testFiles.ToArray());
            Assert.Equal(2, usages.Count());

            var regularResult = usages.ElementAt(0);
            var mappedResult = usages.ElementAt(1);

            Assert.Equal("a.cs", regularResult.Uri);
            Assert.Equal("a.cs", mappedResult.Uri);

            Assert.Equal(3, regularResult.Range.Start.Line);
            Assert.Equal(11, mappedResult.Range.Start.Line);

            // Assert.Equal("public void bar() { }", regularResult.Text);
            // Assert.Equal("new Foo().bar();", mappedResult.Text);

            Assert.Equal(32, regularResult.Range.Start.Character);
            Assert.Equal(35, regularResult.Range.End.Character);
            Assert.Equal(34, mappedResult.Range.Start.Character);
            Assert.Equal(37, mappedResult.Range.End.Character);
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
            Assert.Single(usages);
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
            Assert.Equal(2, usages.Count());
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
            Assert.Equal(3, usages.Count());
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
            Assert.Equal(2, usages.Count());
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
            Assert.Equal(2, usages.Count());
        }

        private Task<LocationContainer> FindUsagesAsync(string code, bool excludeDefinition = false)
        {
            return FindUsagesAsync(new[] { new TestFile("dummy.cs", code) }, excludeDefinition);
        }

        private async Task<LocationContainer> FindUsagesAsync(TestFile[] testFiles, bool excludeDefinition = false)
        {
            OmniSharpTestHost.AddFilesToWorkspace(testFiles
                .Select(f =>
                    new TestFile(
                        ((f.FileName.StartsWith("/") || f.FileName.StartsWith("\\")) ? f.FileName : ("/" + f.FileName))
                        .Replace(Path.AltDirectorySeparatorChar, Path.DirectorySeparatorChar), f.Content))
                .ToArray()
            );
            var file = testFiles.Single(tf => tf.Content.HasPosition);
            var point = file.Content.GetPointFromPosition();

            Client.TextDocument.DidChangeTextDocument(new DidChangeTextDocumentParams()
            {
                ContentChanges = new Container<TextDocumentContentChangeEvent>(new TextDocumentContentChangeEvent()
                {
                    Text = file.Content.Code
                }),
                TextDocument = new OptionalVersionedTextDocumentIdentifier()
                {
                    Uri = DocumentUri.From(file.FileName),
                    Version = 1
                }
            });
            return await Client.TextDocument.RequestReferences(new ReferenceParams()
            {
                Position = new Position(point.Line, point.Offset),
                TextDocument = new TextDocumentIdentifier(DocumentUri.From(file.FileName)),
                Context = new ReferenceContext { IncludeDeclaration = !excludeDefinition }
            }, CancellationToken);
        }
    }
}
