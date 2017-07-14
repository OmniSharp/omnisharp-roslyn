using System.Threading.Tasks;
using OmniSharp.Cake.Utilities;
using OmniSharp.Models;

namespace OmniSharp.Cake.Extensions
{
    internal static class RequestExtensions
    {
        public static async Task<TRequest> TranslateAsync<TRequest>(this TRequest request, OmniSharpWorkspace workspace) where TRequest : Request
        {
            var offset = await GetOffset(request.FileName, workspace);

            request.Line += offset;

            return request;
        }

        private static async Task<int> GetOffset(string fileName, OmniSharpWorkspace workspace)
        {
            return await LineOffsetHelper.GetOffset(fileName, workspace) + 1;
        }
    }
}
