using System.Linq;
using System.Threading.Tasks;
using OmniSharp.Models;
using OmniSharp.Models.FindSymbols;
using OmniSharp.Roslyn.CSharp.Services.Navigation;
using Microsoft.CodeAnalysis;
using TestUtility;
using Xunit;
using Xunit.Abstractions;

namespace OmniSharp.Roslyn.CSharp.Tests
{
    public class FindSymbolsFacts : AbstractSingleRequestHandlerTestFixture<FindSymbolsService>
    {
        public FindSymbolsFacts(ITestOutputHelper output, SharedOmniSharpHostFixture sharedOmniSharpHostFixture)
            : base(output, sharedOmniSharpHostFixture)
        {
        }

        protected override string EndpointName => OmniSharpEndpoints.FindSymbols;

        [Theory]
        [InlineData("dummy.cs")]
        [InlineData("dummy.csx")]
        public async Task Can_find_symbols(string filename)
        {
            string code = @"
                    public class Foo
                    {
                        private string _field = 0;
                        private string AutoProperty { get; }
                        private string Property
                        {
                            get { return _field; }
                            set { _field = value; }
                        }
                        private string Method() {}
                        private string Method(string param) {}

                        private class Nested
                        {
                            private string NestedMethod() {}
                        }
                    }";

            code = WrapInNamespaceIfNeeded(code, filename);
            var usages = await FindSymbolsAsync(code, filename);
            var symbols = usages.QuickFixes.Select(q => q.Text);

            var expected = new[]
            {
                "Foo",
                "_field",
                "AutoProperty",
                "Property",
                "Method()",
                "Method(string param)",
                "Nested",
                "NestedMethod()"
            };

            Assert.Equal(expected, symbols);
        }

        [Theory]
        [InlineData("dummy.cs")]
        [InlineData("dummy.csx")]
        public async Task Does_not_return_event_keyword(string filename)
        {
            const string code = @"
                public static class Game
                {
                    public static event GameEvent GameResumed;
                }";

            var usages = await FindSymbolsAsync(code, filename);
            var symbols = usages.QuickFixes.Select(q => q.Text);

            var expected = new[]
            {
                "Game",
                "GameResumed"
            };

            Assert.Equal(expected, symbols);
        }

        [Theory]
        [InlineData("dummy.cs")]
        [InlineData("dummy.csx")]
        public async Task Can_find_symbols_kinds(string filename)
        {
            string code = @"
                    public class Foo
                    {
                        private string _field = 0;
                        private string AutoProperty { get; }
                        private string Property
                        {
                            get { return _field; }
                            set { _field = value; }
                        }
                        private string Method() {}
                        private string Method(string param) {}

                        private class Nested
                        {
                            private string NestedMethod() {}
                        }
                    }";

            code = WrapInNamespaceIfNeeded(code, filename);
            var usages = await FindSymbolsAsync(code, filename);
            var symbols = usages.QuickFixes.Cast<SymbolLocation>().Select(q => q.Kind);

            var expected = new[]
            {
                "Class",
                "Field",
                "Property",
                "Property",
                "Method",
                "Method",
                "Class",
                "Method"
            };

            Assert.Equal(expected, symbols);
        }

        [Theory]
        [InlineData("dummy.cs")]
        [InlineData("dummy.csx")]
        public async Task Returns_interface_kind(string filename)
        {
            const string code = @"public interface Foo {}";

            var usages = await FindSymbolsAsync(code, filename);
            var symbols = usages.QuickFixes.Cast<SymbolLocation>().Select(q => q.Kind);
            Assert.Equal("Interface", symbols.First());
        }

        [Theory]
        [InlineData("dummy.cs")]
        [InlineData("dummy.csx")]
        public async Task Returns_enum_kind(string filename)
        {
            const string code = @"public enum Foo {}";

            var usages = await FindSymbolsAsync(code, filename);
            var symbols = usages.QuickFixes.Cast<SymbolLocation>().Select(q => q.Kind);
            Assert.Equal("Enum", symbols.First());
        }

        [Theory]
        [InlineData("dummy.cs")]
        [InlineData("dummy.csx")]
        public async Task Returns_struct_kind(string filename)
        {
            const string code = @"public struct Foo {}";

            var usages = await FindSymbolsAsync(code, filename);
            var symbols = usages.QuickFixes.Cast<SymbolLocation>().Select(q => q.Kind);
            Assert.Equal("Struct", symbols.First());
        }

        [Theory]
        [InlineData("dummy.cs")]
        [InlineData("dummy.csx")]
        public async Task Returns_delegate_kind(string filename)
        {
            const string code = @"public delegate void Foo();";

            var usages = await FindSymbolsAsync(code, filename);
            var symbols = usages.QuickFixes.Cast<SymbolLocation>().Select(q => q.Kind);
            Assert.Equal("Delegate", symbols.First());
        }

        [Theory]
        [InlineData("dummy.cs")]
        [InlineData("dummy.csx")]
        public async Task Finds_partial_method_with_body(string filename)
        {
            const string code = @"
public partial class MyClass  
{
    partial void Method();
}

public partial class MyClass 
{
    partial void Method()
    {
       // do stuff
    }
}";

            var usages = await FindSymbolsAsync(code, filename);
            var methodSymbol = usages.QuickFixes.Cast<SymbolLocation>().First(x => x.Kind == SymbolKind.Method.ToString());

            // should find the occurrance with body
            Assert.Equal(8, methodSymbol.Line);
        }

        [Theory]
        [InlineData("dummy.cs")]
        [InlineData("dummy.csx")]
        public async Task Can_find_symbols_using_filter(string filename)
        {
            string code = @"
                    public class Foo
                    {
                        private string _field = 0;
                        private string AutoProperty { get; }
                        private string Property
                        {
                            get { return _field; }
                            set { _field = value; }
                        }
                        private void Method() {}
                        private string Method(string param) {}

                        private class Nested
                        {
                            private string NestedMethod() {}
                        }
                    }";

            code = WrapInNamespaceIfNeeded(code, filename);
            var usages = await FindSymbolsWithFilterAsync(code, filename, "meth", minFilterLength: null, maxItemsToReturn: null);
            var symbols = usages.QuickFixes.Select(q => q.Text);

            var expected = new[]
            {
                "Method()",
                "Method(string param)",
                "NestedMethod()"
            };

            Assert.Equal(expected, symbols);
        }

        [Theory]
        [InlineData("dummy.cs")]
        [InlineData("dummy.csx")]
        public async Task Can_find_symbols_using_filter_with_subset_match(string filename)
        {
            string code = @"
                    public class Options {}
                    public class Opossum {}
                    public interface IConfigurationOptions { }
                    public class ConfigurationOptions : IConfigurationOptions { }";

            code = WrapInNamespaceIfNeeded(code, filename);
            var usages = await FindSymbolsWithFilterAsync(code, filename, "opti", minFilterLength: 0, maxItemsToReturn: 0);
            var symbols = usages.QuickFixes.Select(q => q.Text);

            var expected = new[]
            {
                "Options",
                "IConfigurationOptions",
                "ConfigurationOptions"
            };

            Assert.Equal(expected, symbols);
        }

        [Theory]
        [InlineData("dummy.cs")]
        [InlineData("dummy.csx")]
        public async Task no_symbols_returned_when_filter_too_short(string filename)
        {
            string code = @"
                    public class Options {}";

            code = WrapInNamespaceIfNeeded(code, filename);
            var usages = await FindSymbolsWithFilterAsync(code, filename, "op", minFilterLength: 3, maxItemsToReturn: 0);
            var symbols = usages.QuickFixes.Select(q => q.Text);

            Assert.Empty(symbols);
        }

        [Theory]
        [InlineData("dummy.cs")]
        [InlineData("dummy.csx")]
        public async Task limit_number_of_returned_symbols(string filename)
        {
            string code = @"
                    public class Options1 {}
                    public class Options2 {}
                    public class Options3 {}";

            code = WrapInNamespaceIfNeeded(code, filename);
            var usages = await FindSymbolsWithFilterAsync(code, filename, "op", minFilterLength: 0, maxItemsToReturn: 2);
            var symbols = usages.QuickFixes.Select(q => q.Text);

            Assert.Equal(2, symbols.Count());
        }

        [Theory]
        [InlineData("dummy.cs")]
        [InlineData("dummy.csx")]
        public async Task fuzzy_search(string filename)
        {
            string code = @"
                    public class ProjectManager {}
                    public class CoolProjectManager {}
                    public class ProbabilityManager {}";

            code = WrapInNamespaceIfNeeded(code, filename);
            var usages = await FindSymbolsWithFilterAsync(code, filename, "ProjMana", minFilterLength: 0, maxItemsToReturn: 0);
            var symbols = usages.QuickFixes.Select(q => q.Text);
            Assert.Contains("ProjectManager", symbols);
            Assert.Contains("CoolProjectManager", symbols);
            Assert.DoesNotContain("ProbabilityManager", symbols);
        }

        private async Task<QuickFixResponse> FindSymbolsAsync(string code, string filename)
        {
            var testFile = new TestFile(filename, code);
            SharedOmniSharpTestHost.AddFilesToWorkspace(testFile);
            var requestHandler = GetRequestHandler(SharedOmniSharpTestHost);

            return await requestHandler.Handle(null);
        }

        private async Task<QuickFixResponse> FindSymbolsWithFilterAsync(string code, string filename, string filter, int? minFilterLength, int? maxItemsToReturn)
        {
            var testFile = new TestFile(filename, code);
            SharedOmniSharpTestHost.AddFilesToWorkspace(testFile);
            var requestHandler = GetRequestHandler(SharedOmniSharpTestHost);

            return await requestHandler.Handle(new FindSymbolsRequest {
                Filter = filter,
                MinFilterLength = minFilterLength,
                MaxItemsToReturn = maxItemsToReturn
            });
        }

        private string WrapInNamespaceIfNeeded(string code, string filename)
        {
            if (filename.EndsWith(".cs"))
            {
                code = @"
                namespace Some.Long.Namespace
                {
                    " + code + @"
                }";
            }

            return code;
        }
    }
}
