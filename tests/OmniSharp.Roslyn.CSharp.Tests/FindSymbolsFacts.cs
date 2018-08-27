using System.Linq;
using System.Threading.Tasks;
using OmniSharp.Models;
using OmniSharp.Models.FindSymbols;
using OmniSharp.Roslyn.CSharp.Services.Navigation;
using Microsoft.CodeAnalysis;
using TestUtility;
using Xunit;
using Xunit.Abstractions;
using System.Collections.Generic;
using OmniSharp.Options;

namespace OmniSharp.Roslyn.CSharp.Tests
{
    public class FindSymbolsFacts : AbstractSingleRequestHandlerTestFixture<FindSymbolsService>
    {
        public FindSymbolsFacts(ITestOutputHelper output)
            : base(output)
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

            using (var testHost = CreateOmniSharpHost())
            {
                var usages = await FindSymbolsAsync(code, testHost);
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
        }

        [Fact]
        public async Task Does_not_return_event_keyword()
        {
            const string code = @"
                public static class Game
                {
                    public static event GameEvent GameResumed;
                }";

            using (var testHost = CreateOmniSharpHost())
            {
                var usages = await FindSymbolsAsync(code, testHost);
                var symbols = usages.QuickFixes.Select(q => q.Text);

                var expected = new[]
                {
                    "Game",
                    "GameResumed"
                };

                Assert.Equal(expected, symbols);
            }
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

            using (var testHost = CreateOmniSharpHost())
            {
                var usages = await FindSymbolsAsync(code, testHost);
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
        }

        [Fact]
        public async Task Returns_interface_kind()
        {
            const string code = @"public interface Foo {}";

            using (var testHost = CreateOmniSharpHost())
            {
                var usages = await FindSymbolsAsync(code, testHost);
                var symbols = usages.QuickFixes.Cast<SymbolLocation>().Select(q => q.Kind);
                Assert.Equal("Interface", symbols.First());
            }
        }

        [Fact]
        public async Task Returns_enum_kind()
        {
            const string code = @"public enum Foo {}";

            using (var testHost = CreateOmniSharpHost())
            {
                var usages = await FindSymbolsAsync(code, testHost);
                var symbols = usages.QuickFixes.Cast<SymbolLocation>().Select(q => q.Kind);
                Assert.Equal("Enum", symbols.First());
            }
        }

        [Fact]
        public async Task Returns_struct_kind()
        {
            const string code = @"public struct Foo {}";

            using (var testHost = CreateOmniSharpHost())
            {
                var usages = await FindSymbolsAsync(code, testHost);
                var symbols = usages.QuickFixes.Cast<SymbolLocation>().Select(q => q.Kind);
                Assert.Equal("Struct", symbols.First());
            }
        }

        [Fact]
        public async Task Returns_delegate_kind()
        {
            const string code = @"public delegate void Foo();";

            using (var testHost = CreateOmniSharpHost())
            {
                var usages = await FindSymbolsAsync(code, testHost);
                var symbols = usages.QuickFixes.Cast<SymbolLocation>().Select(q => q.Kind);
                Assert.Equal("Delegate", symbols.First());
            }
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

            using (var testHost = CreateOmniSharpHost())
            {
                var usages = await FindSymbolsAsync(code, testHost);
                var methodSymbol = usages.QuickFixes.Cast<SymbolLocation>().First(x => x.Kind == SymbolKind.Method.ToString());

                // should find the occurrance with body
                Assert.Equal(8, methodSymbol.Line);
            }
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

            using (var testHost = CreateOmniSharpHost())
            {
                var usages = await FindSymbolsWithFilterAsync(code, "meth", testHost);
                var symbols = usages.QuickFixes.Select(q => q.Text);

                var expected = new[]
                {
                    "Method()",
                    "Method(string param)",
                    "NestedMethod()"
                };

                Assert.Equal(expected, symbols);
            }
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

            using (var testHost = CreateOmniSharpHost())
            {
                var usages = await FindSymbolsWithFilterAsync(code, "opti", testHost);
                var symbols = usages.QuickFixes.Select(q => q.Text);

                var expected = new[]
                {
                    "Options",
                    "IConfigurationOptions",
                    "ConfigurationOptions"
                };

                Assert.Equal(expected, symbols);
            }
        }

        [Fact]
        public async Task no_symbols_returned_when_filter_too_short()
        {
            const string code = @"
                namespace Some.Namespace
                {
                    public class Options {}
                }";

            var configData  = new Dictionary<string, string>
            {
                [$"{nameof(OmniSharpOptions.FindSymbols)}:{nameof(FindSymbolsOptions.MinFilterLength)}"] = "3"
            };

            using (var testHost = CreateOmniSharpHost(configurationData: configData))
            {
                var usages = await FindSymbolsWithFilterAsync(code, "op", testHost);
                var symbols = usages.QuickFixes.Select(q => q.Text);

                Assert.Empty(symbols);
            }
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

            var configData = new Dictionary<string, string>
            {
                [$"{nameof(OmniSharpOptions.FindSymbols)}:{nameof(FindSymbolsOptions.MaxItemsToReturn)}"] = "2"
            };

            using (var testHost = CreateOmniSharpHost(configurationData: configData))
            {
                var usages = await FindSymbolsWithFilterAsync(code, "op", testHost);
                var symbols = usages.QuickFixes.Select(q => q.Text);

                Assert.Equal(2, symbols.Count());
            }
        }

        private async Task<QuickFixResponse> FindSymbolsAsync(string code, OmniSharpTestHost testHost)
        {
            var testFile = new TestFile("dummy.cs", code);

            testHost.AddFilesToWorkspace(testFile);
            var requestHandler = GetRequestHandler(testHost);

            return await requestHandler.Handle(null);
        }

        private async Task<QuickFixResponse> FindSymbolsWithFilterAsync(string code, string filter, OmniSharpTestHost testHost)
        {
            var testFile = new TestFile("dummy.cs", code);
            testHost.AddFilesToWorkspace(testFile);
            var requestHandler = GetRequestHandler(testHost);

            return await requestHandler.Handle(new FindSymbolsRequest { Filter = filter });
        }
    }
}
