using System.Threading.Tasks;
using OmniSharp.Cake.Utilities;
using OmniSharp.Models.V2.CodeActions;

namespace OmniSharp.Cake.Services.RequestHandlers.Refactoring.V2
{
    public abstract class BaseCodeActionsHandler<TRequest, TResponse> : CakeRequestHandler<TRequest, TResponse>
        where TRequest : ICodeActionRequest
    {
        protected BaseCodeActionsHandler(
            OmniSharpWorkspace workspace)
            : base(workspace)
        {
        }

        protected override async Task<TRequest> TranslateRequestAsync(TRequest request)
        {
            if (request.Selection != null)
            {
                var startLine = await LineIndexHelper.TranslateToGenerated(request.FileName, request.Selection.Start.Line, Workspace);
                var endLine = request.Selection.Start.Line != request.Selection.End.Line
                    ? await LineIndexHelper.TranslateToGenerated(request.FileName, request.Selection.End.Line, Workspace)
                    : startLine;
                request = (TRequest)request.WithSelection(
                    request.Selection with
                    {
                        Start = request.Selection.Start with { Line = startLine },
                        End = request.Selection.End with { Line = endLine }
                    });
            }

            return await base.TranslateRequestAsync(request);
        }
    }
}
