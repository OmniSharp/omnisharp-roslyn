using System.Linq;
using System.Threading.Tasks;
using OmniSharp.Models;
using Xunit;

namespace OmniSharp.Tests
{
    public class FindSymbolsFacts
    {
        [Fact]
        public async Task Can_find_symbols()
        {
            var source = @"
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

            var usages = await FindSymbols(source);
            var symbols = usages.QuickFixes.Select(q => q.Text);

            var expected = new[] {
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
            var source = @"
                public static class Game
                {
                    public static event GameEvent GameResumed;
                }";

            var usages = await FindSymbols(source);
            var symbols = usages.QuickFixes.Select(q => q.Text);

            var expected = new[] {
                "Game",
                "GameResumed"
            };
            Assert.Equal(expected, symbols);
        }

        [Fact]
        public async Task Can_find_symbols_kinds()
        {
            var source = @"
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

            var usages = await FindSymbols(source);
            var symbols = usages.QuickFixes.Cast<SymbolLocation>().Select(q => q.Kind);

            var expected = new[] {
                "NamedType",
                "Field",
                "Property",
                "Property",
                "Method",
                "Method",
                "NamedType",
                "Method"
            };
            Assert.Equal(expected, symbols);
        }

        [Fact]
        public async Task Can_find_symbols_using_filter()
        {
            var source = @"
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

            var usages = await FindSymbolsWithFilter(source, "meth");
            var symbols = usages.QuickFixes.Select(q => q.Text);

            var expected = new[] {
                "Method()",
                "Method(string param)"
            };
            Assert.Equal(expected, symbols);
        }

        private async Task<QuickFixResponse> FindSymbols(string source)
        {
            var workspace = TestHelpers.CreateSimpleWorkspace(source);
            var controller = new OmnisharpController(workspace, null);
            return await controller.FindSymbols();
        }

        private async Task<QuickFixResponse> FindSymbolsWithFilter(string source, string filter)
        {
            var workspace = TestHelpers.CreateSimpleWorkspace(source);
            var controller = new OmnisharpController(workspace, null);
            var request = new FindSymbolsRequest { Filter = filter };
            return await controller.FindSymbols(request);
        }
    }
}