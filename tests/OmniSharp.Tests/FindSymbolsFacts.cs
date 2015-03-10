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

        private async Task<QuickFixResponse> FindSymbols(string source)
        {
            var workspace = TestHelpers.CreateSimpleWorkspace(source);
            var controller = new OmnisharpController(workspace, null);
            return await controller.FindSymbols();
        }
    }
}