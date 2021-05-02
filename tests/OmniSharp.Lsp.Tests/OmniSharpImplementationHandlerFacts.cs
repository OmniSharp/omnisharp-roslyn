using System.IO;
using System.Linq;
using System.Threading.Tasks;
using OmniSharp.Extensions.LanguageServer.Protocol;
using OmniSharp.Extensions.LanguageServer.Protocol.Document;
using OmniSharp.Extensions.LanguageServer.Protocol.Models;
using TestUtility;
using Xunit;
using Xunit.Abstractions;

namespace OmniSharp.Lsp.Tests
{
    public class OmniSharpImplementationHandlerFacts : AbstractLanguageServerTestBase
    {
        public OmniSharpImplementationHandlerFacts(ITestOutputHelper output)
            : base(output)
        {
        }

        [Fact]
        public async Task CanFindImplementationsOfClass()
        {
            const string code = @"
                public class Foo$$Base
                {
                }

                public class FooDerivedA : FooBase
                {
                }

                public class FooDerivedB : FooBase
                {
                }";

            var implementations = await FindImplementationsAsync(code);
            Assert.Equal(3, implementations.Count());
        }

        [Fact]
        public async Task CanFindImplementationsOfInterface()
        {
            const string code = @"
                public interface IF$$oo
                {
                }

                public class FooA : IFoo
                {
                }

                public class FooB : IFoo
                {
                }";

            var implementations = await FindImplementationsAsync(code);
            Assert.Equal(2, implementations.Count());
        }

        [Fact]
        public async Task CanFindImplementationsOfVirtualFunction()
        {
            const string code = @"
                public class FooBase
                {
                    public virtual int B$$ar() { return 1; }
                }

                public class FooDerivedA : FooBase
                {
                    public override int Bar() { return 2; }
                }

                public class FooDerivedB : FooBase
                {
                    public override int Bar() { return 3; }
                }";

            var implementations = await FindImplementationsAsync(code);
            Assert.Equal(3, implementations.Count());
        }

        [Fact]
        public async Task CanFindImplementationsOfAbstractFunction()
        {
            const string code = @"
                public abstract class FooBase
                {
                    public abstract int B$$ar();
                }

                public class FooDerivedA : FooBase
                {
                    public override int Bar() { return 2; }
                }

                public class FooDerivedB : FooBase
                {
                    public override int Bar() { return 3; }
                }";

            var implementations = await FindImplementationsAsync(code);
            Assert.Equal(2, implementations.Count());
        }

        [Fact]
        public async Task CanFindImplementationsOfVirtualProperty()
        {
            const string code = @"
                public class FooBase
                {
                    public virtual int B$$ar => 1;
                }

                public class FooDerivedA : FooBase
                {
                    public override int Bar => 2;
                }

                public class FooDerivedB : FooBase
                {
                    public override int Bar => 3;
                }";

            var implementations = await FindImplementationsAsync(code);
            Assert.Equal(3, implementations.Count());
        }

        [Fact]
        public async Task CanFindImplementationsOfAbstractProperty()
        {
            const string code = @"
                public abstract class FooBase
                {
                    public abstract int B$$ar { get; }
                }

                public class FooDerivedA : FooBase
                {
                    public override int Bar => 2;
                }

                public class FooDerivedB : FooBase
                {
                    public override int Bar => 3;
                }";

            var implementations = await FindImplementationsAsync(code);
            Assert.Equal(2, implementations.Count());
        }

        [Fact]
        public async Task CannotFindImplementationsWithoutSymbol()
        {
            const string code = @"
                public class Foo
                {
                    $$
                }";

            var implementations = await FindImplementationsAsync(code);
            Assert.Empty(implementations);
        }

        [Fact]
        public async Task CannotFindImplementationsForUnsupportedSymbol()
        {
            const string code = @"
                pub$$lic class Foo
                {
                }";

            var implementations = await FindImplementationsAsync(code);
            Assert.Empty(implementations);
        }

        [Fact]
        public async Task CannotFindImplementationsForEmptyFiles()
        {
            var response = await Client.TextDocument.RequestImplementation(new ImplementationParams
            {
                Position = (0, 0),
                TextDocument = "notfound.cs"
            }, CancellationToken);

            Assert.Empty(response);
        }

        private Task<LocationOrLocationLinks> FindImplementationsAsync(string code)
        {
            return FindImplementationsAsync(new[] { new TestFile("dummy.cs", code) });
        }

        private async Task<LocationOrLocationLinks> FindImplementationsAsync(TestFile[] testFiles)
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

            Client.TextDocument.DidChangeTextDocument(new DidChangeTextDocumentParams
            {
                ContentChanges = new Container<TextDocumentContentChangeEvent>(new TextDocumentContentChangeEvent
                {
                    Text = file.Content.Code
                }),
                TextDocument = new OptionalVersionedTextDocumentIdentifier
                {
                    Uri = DocumentUri.From(file.FileName),
                    Version = 1
                }
            });

            return await Client.TextDocument.RequestImplementation(new ImplementationParams
            {
                Position = new Position(point.Line, point.Offset),
                TextDocument = new TextDocumentIdentifier(DocumentUri.From(file.FileName))
            }, CancellationToken);
        }
    }
}
