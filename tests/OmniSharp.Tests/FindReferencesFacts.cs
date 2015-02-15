﻿using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Xunit;
using OmniSharp.Filters;
using OmniSharp.Models;

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

        [Fact]
        public async Task LimitReferenceSearchToThisFile()
        {
            var sourceA = @"public class F$oo {
                public Foo Clone() {
                    return null;
                }
            }";
            var sourceB = @"public class Bar : Foo {}";

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
            var sourceA = @"public class Bar : F$oo {}";
            var sourceB = @"public class Foo {
                public Foo Clone() {
                    return null;
                }
            }";

            var usages = await FindUsages(new Dictionary<string, string> { { "a.cs", sourceA }, { "b.cs", sourceB } }, "a.cs", true);
            Assert.Equal(1, usages.QuickFixes.Count());
            Assert.Equal("a.cs", usages.QuickFixes.ElementAt(0).FileName);
        }

        private FindUsagesRequest CreateRequest(string source, string fileName = "dummy.cs")
        {
            var lineColumn = TestHelpers.GetLineAndColumnFromDollar(source);
            return new FindUsagesRequest
            {
                Line = lineColumn.Line,
                Column = lineColumn.Column,
                FileName = fileName,
                Buffer = source.Replace("$", ""),
                OnlyThisFile = false
            };
        }

        private async Task<QuickFixResponse> FindUsages(string source)
        {
            return await FindUsages(new Dictionary<string, string> { { "dummy.cs", source }}, "dummy.cs", false);
        }

        private async Task<QuickFixResponse> FindUsages(Dictionary<string, string> sources, string currentFile, bool onlyThisFile)
        {
            var workspace = TestHelpers.CreateSimpleWorkspace(sources);
            var controller = new OmnisharpController(workspace, null);
            var request = CreateRequest(sources[currentFile], currentFile);
            request.OnlyThisFile = onlyThisFile;
            var bufferFilter = new UpdateBufferFilter(workspace);
            bufferFilter.OnActionExecuting(TestHelpers.CreateActionExecutingContext(request));
            return await controller.FindUsages(request);
        }
    }
}