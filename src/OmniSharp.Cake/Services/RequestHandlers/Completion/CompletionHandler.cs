using System.Composition;
using OmniSharp.Mef;
using OmniSharp.Models.v1.Completion;

namespace OmniSharp.Cake.Services.RequestHandlers.Completion
{
    [Shared]
    [OmniSharpHandler(OmniSharpEndpoints.Completion, Constants.LanguageNames.Cake)]
    public class CompletionHandler : CakeRequestHandler<CompletionRequest, CompletionResponse>
    {
        [ImportingConstructor]
        public CompletionHandler(OmniSharpWorkspace workspace) : base(workspace)
        {
        }
    }

    [Shared]
    [OmniSharpHandler(OmniSharpEndpoints.CompletionResolve, Constants.LanguageNames.Cake)]
    public class CompletionResolveHandler : CakeRequestHandler<CompletionResolveRequest, CompletionResolveResponse>
    {
        [ImportingConstructor]
        public CompletionResolveHandler(OmniSharpWorkspace workspace) : base(workspace)
        {
        }
    }
}
