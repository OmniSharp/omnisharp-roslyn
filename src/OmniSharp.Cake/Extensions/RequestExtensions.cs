using System.Collections.Generic;
using System.Threading.Tasks;
using OmniSharp.Cake.Utilities;
using OmniSharp.Models;

namespace OmniSharp.Cake.Extensions
{
    internal static class RequestExtensions
    {
        public static async Task<TRequest> TranslateAsync<TRequest>(this TRequest request, OmniSharpWorkspace workspace) where TRequest : Request
        {
            request.Line = await LineIndexHelper.TranslateToGenerated(request.FileName, request.Line, workspace);

            if (request.Changes == null)
            {
                return request;
            }

            var changes = new List<LinePositionSpanTextChange>();
            foreach (var change in request.Changes)
            {
                var oldStartLine = change.StartLine;
                change.StartLine = await LineIndexHelper.TranslateToGenerated(request.FileName, change.StartLine, workspace);
                change.EndLine = oldStartLine == change.EndLine ?
                    change.StartLine :
                    await LineIndexHelper.TranslateToGenerated(request.FileName, change.EndLine, workspace);

                changes.Add(change);
            }
            request.Changes = changes;

            return request;
        }
    }
}
