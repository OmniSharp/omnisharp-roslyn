using System.Linq;
using System.Threading.Tasks;
using Xunit;
using OmniSharp.Models;
using OmniSharp.Filters;

namespace OmniSharp.Tests
{
    public class FindReferencesFacts
    {
        [Fact]
        public async Task CanFindReferencesOfLocalVariable()
        {
            var source = @"
                public class Foo
                {
                    public Foo(string s)
                    {
                        var pr$op = s + 'abc';

                        prop += 'woo';
                    }
                }";

            var usages = await FindUsages(source);
            Assert.Equal(2, usages.QuickFixes.Count());
        }

        [Fact]
        public async Task CanFindReferencesOfMethodParameter()
        {
            var source = @"
                public class Foo
                {
                    public Foo(string $s)
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
            var source = @"public class Foo
                {
                    public string p$rop;
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
            //better to be safe than sorry, didn't wanna lump this in with the methods

            var source = @"public class Foo
                {
                    public F$oo() {}
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
            var source = @"public class Foo
                {
                    public void b$ar() { }
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
        public async Task CanFindReferencesOfPublicAutoProperty()
        {
            var source = @"public class Foo
                {
                    public string p$rop {get;set;}
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
            var source = @"public class F$oo
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

        private Request CreateRequest(string source, string fileName = "dummy.cs")
        {
            var lineColumn = TestHelpers.GetLineAndColumnFromDollar(source);
            return new Request
            {
                Line = lineColumn.Line,
                Column = lineColumn.Column,
                FileName = fileName,
                Buffer = source.Replace("$", "")
            };
        }

        private async Task<QuickFixResponse> FindUsages(string source)
        {
            var workspace = TestHelpers.CreateSimpleWorkspace(source);
            var controller = new OmnisharpController(workspace, null);
            var request = CreateRequest(source);
            var bufferFilter = new UpdateBufferFilter(workspace);
            bufferFilter.OnActionExecuting(TestHelpers.CreateActionExecutingContext(request));
            return await controller.FindUsages(request);
        }
    }
}