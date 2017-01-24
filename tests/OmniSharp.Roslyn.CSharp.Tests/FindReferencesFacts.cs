using System.Linq;
using System.Threading.Tasks;
using OmniSharp.Models;
using OmniSharp.Roslyn.CSharp.Services.Navigation;
using TestUtility;
using Xunit;
using Xunit.Abstractions;

namespace OmniSharp.Roslyn.CSharp.Tests
{
    public class FindReferencesFacts : AbstractTestFixture
    {
        public FindReferencesFacts(ITestOutputHelper output)
            : base(output)
        {
        }

        [Fact]
        public async Task CanFindReferencesOfLocalVariable()
        {
            const string source = @"
                public class Foo
                {
                    public Foo(string s)
                    {
                        var pr$$op = s + 'abc';

                        prop += 'woo';
                    }
                }";

            var usages = await FindUsages(source);
            Assert.Equal(2, usages.QuickFixes.Count());
        }

        [Fact]
        public async Task CanFindReferencesOfMethodParameter()
        {
            const string source = @"
                public class Foo
                {
                    public Foo(string $$s)
                    {
                        var prop = s + 'abc';
                    }
                }";

            var usages = await FindUsages(source);
            Assert.Equal(2, usages.QuickFixes.Count());
        }

        [Fact]
        public async Task CanFindReferencesOfField()
        {
            const string source = @"
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

            var usages = await FindUsages(source);
            Assert.Equal(2, usages.QuickFixes.Count());
        }

        [Fact]
        public async Task CanFindReferencesOfConstructor()
        {
            const string source = @"
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

            var usages = await FindUsages(source);
            Assert.Equal(2, usages.QuickFixes.Count());
        }

        [Fact]
        public async Task CanFindReferencesOfMethod()
        {
            const string source = @"
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

            var usages = await FindUsages(source);
            Assert.Equal(2, usages.QuickFixes.Count());
        }

        [Fact]
        public async Task ExcludesMethodDefinition()
        {
            const string source = @"
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

            var usages = await FindUsages(source, excludeDefinition: true);
            Assert.Equal(1, usages.QuickFixes.Count());
        }

        [Fact]
        public async Task CanFindReferencesOfPublicAutoProperty()
        {
            const string source = @"
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

            var usages = await FindUsages(source);
            Assert.Equal(2, usages.QuickFixes.Count());
        }

        [Fact]
        public async Task CanFindReferencesOfClass()
        {
            const string source = @"
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

            var usages = await FindUsages(source);
            Assert.Equal(2, usages.QuickFixes.Count());
        }

        [Fact]
        public async Task LimitReferenceSearchToThisFile()
        {
            const string sourceA = @"
                public class F$$oo {
                public Foo Clone() {
                    return null;
                }
            }";

            const string sourceB = @"public class Bar : Foo {}";

            var testFiles = new[]
            {
                new TestFile("a.cs", sourceA),
                new TestFile("b.cs", sourceB)
            };

            var usages = await FindUsages(testFiles, onlyThisFile: false);
            Assert.Equal(3, usages.QuickFixes.Count());
            Assert.Equal("a.cs", usages.QuickFixes.ElementAt(0).FileName);
            Assert.Equal("a.cs", usages.QuickFixes.ElementAt(1).FileName);
            Assert.Equal("b.cs", usages.QuickFixes.ElementAt(2).FileName);

            usages = await FindUsages(testFiles, onlyThisFile: true);
            Assert.Equal(2, usages.QuickFixes.Count());
            Assert.Equal("a.cs", usages.QuickFixes.ElementAt(0).FileName);
            Assert.Equal("a.cs", usages.QuickFixes.ElementAt(1).FileName);
        }

        [Fact]
        public async Task DontFindDefinitionInAnotherFile()
        {
            const string sourceA = @"public class Bar : F$$oo {}";
            const string sourceB = @"public class Foo {
                public Foo Clone() {
                    return null;
                }
            }";

            var testFiles = new[]
            {
                new TestFile("a.cs", sourceA),
                new TestFile("b.cs", sourceB)
            };

            var usages = await FindUsages(testFiles, onlyThisFile: true);
            Assert.Equal(1, usages.QuickFixes.Count());
            Assert.Equal("a.cs", usages.QuickFixes.ElementAt(0).FileName);
        }

        private Task<QuickFixResponse> FindUsages(string source, bool excludeDefinition = false)
        {
            return FindUsages(new[] { new TestFile("dummy.cs", source) }, false, excludeDefinition);
        }

        private async Task<QuickFixResponse> FindUsages(TestFile[] testFiles, bool onlyThisFile, bool excludeDefinition = false)
        {
            var file = testFiles.Single(tf => tf.Content.HasPosition);
            var point = file.Content.GetPointFromPosition();

            var workspace = await CreateWorkspaceAsync(testFiles);
            var controller = new FindUsagesService(workspace);

            var request = new FindUsagesRequest
            {
                Line = point.Line,
                Column = point.Offset,
                FileName = file.FileName,
                Buffer = file.Content.Code,
                OnlyThisFile = onlyThisFile,
                ExcludeDefinition = excludeDefinition
            };

            return await controller.Handle(request);
        }
    }
}
