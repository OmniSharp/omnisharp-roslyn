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

            return request;
        }
    }
}
