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
                    }
                }";

            var usages = await FindSymbols(source);
            var symbols = usages.QuickFixes.ToArray();

            Assert.Equal("_field", symbols[0].Text);
            Assert.Equal("AutoProperty", symbols[1].Text);
            Assert.Equal("Property", symbols[2].Text);
            Assert.Equal("Method()", symbols[3].Text);
            Assert.Equal("Method(string param)", symbols[4].Text);
               
        }

        private async Task<QuickFixResponse> FindSymbols(string source)
        {
            var workspace = TestHelpers.CreateSimpleWorkspace(source);
            var controller = new OmnisharpController(workspace, null);
            return await controller.FindSymbols();
        }
    }
}