using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using OmniSharp.Models;
using OmniSharp.Roslyn.CSharp.Services.Navigation;
using TestUtility;
using Xunit;

namespace OmniSharp.Roslyn.CSharp.Tests
{
    public class FindReferencesFacts
    {
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

            var usages = await FindUsages(new Dictionary<string, string> { { "a.cs", sourceA }, { "b.cs", sourceB } }, "a.cs", false);
            Assert.Equal(3, usages.QuickFixes.Count());
            Assert.Equal("a.cs", usages.QuickFixes.ElementAt(0).FileName);
            Assert.Equal("a.cs", usages.QuickFixes.ElementAt(1).FileName);
            Assert.Equal("b.cs", usages.QuickFixes.ElementAt(2).FileName);

            usages = await FindUsages(new Dictionary<string, string> { { "a.cs", sourceA }, { "b.cs", sourceB } }, "a.cs", true);
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

            var usages = await FindUsages(new Dictionary<string, string> { { "a.cs", sourceA }, { "b.cs", sourceB } }, "a.cs", true);
            Assert.Equal(1, usages.QuickFixes.Count());
            Assert.Equal("a.cs", usages.QuickFixes.ElementAt(0).FileName);
        }

        private static Task<QuickFixResponse> FindUsages(string source, bool excludeDefinition = false)
        {
            return FindUsages(new Dictionary<string, string> { { "dummy.cs", source } }, "dummy.cs", false, excludeDefinition);
        }

        private static async Task<QuickFixResponse> FindUsages(Dictionary<string, string> sources, string currentFile, bool onlyThisFile, bool excludeDefinition = false)
        {
            var markup = MarkupCode.Parse(sources[currentFile]);
            var point = markup.Text.GetPointFromPosition(markup.Position);

            var workspace = await TestHelpers.CreateSimpleWorkspace(sources);
            var controller = new FindUsagesService(workspace);

            var request = new FindUsagesRequest
            {
                Line = point.Line,
                Column = point.Offset,
                FileName = currentFile,
                Buffer = markup.Code,
                OnlyThisFile = onlyThisFile,
                ExcludeDefinition = excludeDefinition
            };

            await workspace.BufferManager.UpdateBuffer(request);

            return await controller.Handle(request);
        }
    }
}
