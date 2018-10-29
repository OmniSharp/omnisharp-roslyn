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

        [Fact]
        public async Task Can_find_symbols()
        {
            const string code = @"
                namespace Some.Long.Namespace
                {
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
                    }
                }";

            var usages = await FindSymbolsAsync(code);
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

        [Fact]
        public async Task Does_not_return_event_keyword()
        {
            const string code = @"
                public static class Game
                {
                    public static event GameEvent GameResumed;
                }";

            var usages = await FindSymbolsAsync(code);
            var symbols = usages.QuickFixes.Select(q => q.Text);

            var expected = new[]
            {
                "Game",
                "GameResumed"
            };

            Assert.Equal(expected, symbols);
        }

        [Fact]
        public async Task Can_find_symbols_kinds()
        {
            const string code = @"
                namespace Some.Long.Namespace
                {
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
                    }
                }";

            var usages = await FindSymbolsAsync(code);
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

        [Fact]
        public async Task Returns_interface_kind()
        {
            const string code = @"public interface Foo {}";

            var usages = await FindSymbolsAsync(code);
            var symbols = usages.QuickFixes.Cast<SymbolLocation>().Select(q => q.Kind);
            Assert.Equal("Interface", symbols.First());
        }

        [Fact]
        public async Task Returns_enum_kind()
        {
            const string code = @"public enum Foo {}";

            var usages = await FindSymbolsAsync(code);
            var symbols = usages.QuickFixes.Cast<SymbolLocation>().Select(q => q.Kind);
            Assert.Equal("Enum", symbols.First());
        }

        [Fact]
        public async Task Returns_struct_kind()
        {
            const string code = @"public struct Foo {}";

            var usages = await FindSymbolsAsync(code);
            var symbols = usages.QuickFixes.Cast<SymbolLocation>().Select(q => q.Kind);
            Assert.Equal("Struct", symbols.First());
        }

        [Fact]
        public async Task Returns_delegate_kind()
        {
            const string code = @"public delegate void Foo();";

            var usages = await FindSymbolsAsync(code);
            var symbols = usages.QuickFixes.Cast<SymbolLocation>().Select(q => q.Kind);
            Assert.Equal("Delegate", symbols.First());
        }

        [Fact]
        public async Task Finds_partial_method_with_body()
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

            var usages = await FindSymbolsAsync(code);
            var methodSymbol = usages.QuickFixes.Cast<SymbolLocation>().First(x => x.Kind == SymbolKind.Method.ToString());

            // should find the occurrance with body
            Assert.Equal(8, methodSymbol.Line);
        }

        [Fact]
        public async Task Can_find_symbols_using_filter()
        {
            const string code = @"
                namespace Some.Long.Namespace
                {
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
                    }
                }";

            var usages = await FindSymbolsWithFilterAsync(code, "meth", minFilterLength: null, maxItemsToReturn: null);
            var symbols = usages.QuickFixes.Select(q => q.Text);

            var expected = new[]
            {
                "Method()",
                "Method(string param)",
                "NestedMethod()"
            };

            Assert.Equal(expected, symbols);
        }

        [Fact]
        public async Task Can_find_symbols_using_filter_with_subset_match()
        {
            const string code = @"
                namespace Some.Long.Namespace
                {
                    public class Options {}
                    public class Opossum {}
                    public interface IConfigurationOptions { }
                    public class ConfigurationOptions : IConfigurationOptions { }
                }";

            var usages = await FindSymbolsWithFilterAsync(code, "opti", minFilterLength: 0, maxItemsToReturn: 0);
            var symbols = usages.QuickFixes.Select(q => q.Text);

            var expected = new[]
            {
                "Options",
                "IConfigurationOptions",
                "ConfigurationOptions"
            };

            Assert.Equal(expected, symbols);
        }

        [Fact]
        public async Task no_symbols_returned_when_filter_too_short()
        {
            const string code = @"
                namespace Some.Namespace
                {
                    public class Options {}
                }";

            var usages = await FindSymbolsWithFilterAsync(code, "op", minFilterLength: 3, maxItemsToReturn: 0);
            var symbols = usages.QuickFixes.Select(q => q.Text);

            Assert.Empty(symbols);
        }

        [Fact]
        public async Task limit_number_of_returned_symbols()
        {
            const string code = @"
                namespace Some.Namespace
                {
                    public class Options1 {}
                    public class Options2 {}
                    public class Options3 {}
                }";

            var usages = await FindSymbolsWithFilterAsync(code, "op", minFilterLength: 0, maxItemsToReturn: 2);
            var symbols = usages.QuickFixes.Select(q => q.Text);

            Assert.Equal(2, symbols.Count());
        }

        [Fact]
        public async Task fuzzy_search()
        {
            const string code = @"
                namespace Some.Namespace
                {
                    public class ProjectManager {}
                    public class CoolProjectManager {}
                    public class ProbabilityManager {}
                }";

            var usages = await FindSymbolsWithFilterAsync(code, "ProjMana", minFilterLength: 0, maxItemsToReturn: 0);
            var symbols = usages.QuickFixes.Select(q => q.Text);
            Assert.Contains("ProjectManager", symbols);
            Assert.Contains("CoolProjectManager", symbols);
            Assert.DoesNotContain("ProbabilityManager", symbols);
        }

        private async Task<QuickFixResponse> FindSymbolsAsync(string code)
        {
            var testFile = new TestFile("dummy.cs", code);
            SharedOmniSharpTestHost.AddFilesToWorkspace(testFile);
            var requestHandler = GetRequestHandler(SharedOmniSharpTestHost);

            return await requestHandler.Handle(null);
        }

        private async Task<QuickFixResponse> FindSymbolsWithFilterAsync(string code, string filter, int? minFilterLength, int? maxItemsToReturn)
        {
            var testFile = new TestFile("dummy.cs", code);
            SharedOmniSharpTestHost.AddFilesToWorkspace(testFile);
            var requestHandler = GetRequestHandler(SharedOmniSharpTestHost);

            return await requestHandler.Handle(new FindSymbolsRequest {
                Filter = filter,
                MinFilterLength = minFilterLength,
                MaxItemsToReturn = maxItemsToReturn
            });
        }
    }
}
