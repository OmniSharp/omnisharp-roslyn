using System.Composition;
using System.Threading.Tasks;
using OmniSharp.Cake.Extensions;
using OmniSharp.Cake.Utilities;
using OmniSharp.Mef;
using OmniSharp.Models.SemanticHighlight;

namespace OmniSharp.Cake.Services.RequestHandlers.SemanticHighlight;

[OmniSharpHandler(OmniSharpEndpoints.V2.Highlight, Constants.LanguageNames.Cake), Shared]
public class SemanticHighlightHandler : CakeRequestHandler<SemanticHighlightRequest, SemanticHighlightResponse>
{
    [ImportingConstructor]
    public SemanticHighlightHandler(OmniSharpWorkspace workspace) : base(workspace)
    {
    }

    protected override async Task<SemanticHighlightRequest> TranslateRequestAsync(SemanticHighlightRequest request)
    {
        if (request.Range is not null)
        {
            var startLine = await LineIndexHelper.TranslateToGenerated(request.FileName, request.Range.Start.Line, Workspace);
            var endLine = request.Range.Start.Line != request.Range.End.Line
                ? await LineIndexHelper.TranslateToGenerated(request.FileName, request.Range.End.Line, Workspace)
                : startLine;

            request.Range = request.Range with
            {
                Start = request.Range.Start with { Line = startLine },
                End = request.Range.End with { Line = endLine }
            };
        }

        return await base.TranslateRequestAsync(request);
    }

    protected override Task<SemanticHighlightResponse> TranslateResponse(SemanticHighlightResponse response, SemanticHighlightRequest request)
    {
        return response.TranslateAsync(Workspace, request);
    }
}
