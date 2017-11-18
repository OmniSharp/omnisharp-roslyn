
using System.Composition;
using System.Threading.Tasks;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.FindSymbols;
using Microsoft.CodeAnalysis.Text;
using OmniSharp.Mef;
using OmniSharp.Models.TypeLookup;
using OmniSharp.Options;
using OmniSharp.Roslyn.CSharp.Services.Documentation;

namespace OmniSharp.Models.v2.TypeLookUp
{
    public class TypeLookupResponse
    {
            public string Type { get; set; }
            public DocumentationComment DocComment { get; set; }
    }
    
}
