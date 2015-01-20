using Microsoft.CodeAnalysis;
using OmniSharp.Models;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading.Tasks;
using Xunit;

namespace OmniSharp.Tests
{

    public class foo
    {
        public void bar() { }
    }

    public class FooConsumer
    {
        public FooConsumer()
        {
            new foo().bar();
        }
    }


    public class FindReferencesFacts
    {
        [Fact]
        public async Task CanFindReferencesOfMethod()
        {
            var source = @"    public class foo
                {
                    public void b$ar() { }
                }

                public class FooConsumer
                {
                    public FooConsumer()
                    {
                        new foo().bar();
                    }
                }";
            var useages = await FindUsages(source);
        }

        [Fact]
        public async Task CanFindReferencesOfPublicAutoProperty()
        {
            var source = @"    public class foo
                {
                    public string p$rop {get;set;}
                }

                public class FooConsumer
                {
                    public FooConsumer()
                    {
                        var temp = new foo();
                        var prop = foo.prop;
                    }
                }";

            var workspace = TestHelpers.CreateSimpleWorkspace(source);
            var controller = new OmnisharpController(workspace, null);
            var request = CreateRequest(source);
            var usages = await controller.FindUsages(request);

            Assert.Equal(usages.QuickFixes.Count(), 2);
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

        private async Task<IEnumerable<ISymbol>> FindUsages(string source)
        {
            var workspace = TestHelpers.CreateSimpleWorkspace(source);
            var controller = new OmnisharpController(workspace, null);
            var request = CreateRequest(source);
            var usages = await controller.FindUsages(request);
            return await TestHelpers.SymbolsFromQuickFixes(workspace, usages.QuickFixes);
        }
    }

}