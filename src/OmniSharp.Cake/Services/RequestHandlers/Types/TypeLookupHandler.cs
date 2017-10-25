using System.Composition;
using OmniSharp.Mef;
using OmniSharp.Models.TypeLookup;

namespace OmniSharp.Cake.Services.RequestHandlers.Types
{
    [OmniSharpHandler(OmniSharpEndpoints.TypeLookup, Constants.LanguageNames.Cake), Shared]
    public class TypeLookupHandler : CakeRequestHandler<TypeLookupRequest, TypeLookupResponse>
    {
        [ImportingConstructor]
        public TypeLookupHandler(OmniSharpWorkspace workspace) : base(workspace)
        {
        }
    }
}
