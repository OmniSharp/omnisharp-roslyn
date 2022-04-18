using System.Composition;
using System.Linq;
using System.Threading.Tasks;
using OmniSharp.Cake.Extensions;
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

        protected override Task<CompletionResponse> TranslateResponse(CompletionResponse response, CompletionRequest request)
        {
            if (response.Items is { Count: > 0 })
            {
                // In some instances, formatting changes to generated Cake DSL are being
                // included in the CompletionItem as AdditionalTextEdits. The short term
                // fix is to remove AdditionalTextEdits for now.
                foreach (var item in response.Items)
                {
                    item.AdditionalTextEdits = null;
                }
            }

            return response.TranslateAsync(Workspace, request);
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

        protected override Task<CompletionResolveResponse> TranslateResponse(CompletionResolveResponse response, CompletionResolveRequest request)
        {
            // Due to the fact that AdditionalTextEdits return the complete buffer, we can't currently use that in Cake.
            // Revisit when we have a solution. At this point it's probably just best to remove AdditionalTextEdits.
            if (response.Item is object)
            {
                response.Item.AdditionalTextEdits = null;
            }

            return Task.FromResult(response);
        }
    }
}
