using System.Linq;
using System.Threading.Tasks;
using OmniSharp.Models;
using OmniSharp.Models.FindUsages;
using OmniSharp.Roslyn.CSharp.Services.Navigation;
using TestUtility;
using Xunit;
using Xunit.Abstractions;

namespace OmniSharp.Roslyn.CSharp.Tests
{
    public class FindReferencesFacts : AbstractSingleRequestHandlerTestFixture<FindUsagesService>
    {
        public FindReferencesFacts(ITestOutputHelper output)
            : base(output)
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
            Assert.Equal(1, usages.QuickFixes.Count());
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
            Assert.Equal("a.cs", usages.QuickFixes.ElementAt(0).FileName);
            Assert.Equal("a.cs", usages.QuickFixes.ElementAt(1).FileName);
            Assert.Equal("b.cs", usages.QuickFixes.ElementAt(2).FileName);

            usages = await FindUsagesAsync(testFiles, onlyThisFile: true);
            Assert.Equal(2, usages.QuickFixes.Count());
            Assert.Equal("a.cs", usages.QuickFixes.ElementAt(0).FileName);
            Assert.Equal("a.cs", usages.QuickFixes.ElementAt(1).FileName);
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
            Assert.Equal(1, usages.QuickFixes.Count());
            Assert.Equal("a.cs", usages.QuickFixes.ElementAt(0).FileName);
        }

        private Task<QuickFixResponse> FindUsagesAsync(string code, bool excludeDefinition = false)
        {
            return FindUsagesAsync(new[] { new TestFile("dummy.cs", code) }, false, excludeDefinition);
        }

        private async Task<QuickFixResponse> FindUsagesAsync(TestFile[] testFiles, bool onlyThisFile, bool excludeDefinition = false)
        {
            using (var host = CreateOmniSharpHost(testFiles))
            {
                var file = testFiles.Single(tf => tf.Content.HasPosition);
                var point = file.Content.GetPointFromPosition();

                var requestHandler = GetRequestHandler(host);

                var request = new FindUsagesRequest
                {
                    Line = point.Line,
                    Column = point.Offset,
                    FileName = file.FileName,
                    Buffer = file.Content.Code,
                    OnlyThisFile = onlyThisFile,
                    ExcludeDefinition = excludeDefinition
                };

                return await requestHandler.Handle(request);
            }
        }
    }
}
