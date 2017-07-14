using System.Collections.Generic;
using System.Threading.Tasks;
using OmniSharp.Cake.Utilities;
using OmniSharp.Models;
using OmniSharp.Models.GotoDefinition;
using OmniSharp.Models.Navigate;
using OmniSharp.Models.MembersTree;

namespace OmniSharp.Cake.Extensions
{
    internal static class ResponseExtensions
    {
        public static Task<QuickFixResponse> TranslateAsync(this QuickFixResponse response, OmniSharpWorkspace workspace)
        {
            return response.TranslateAsync(workspace, new Request());
        }

        public static async Task<QuickFixResponse> TranslateAsync(this QuickFixResponse response, OmniSharpWorkspace workspace, Request request)
        {
            var offsets = new Dictionary<string, int>();

            foreach (var quickFix in response.QuickFixes)
            {
                await quickFix.TranslateAsync(workspace, request, offsets);
            }

            return response;
        }

        public static async Task<GotoDefinitionResponse> TranslateAsync(this GotoDefinitionResponse response, OmniSharpWorkspace workspace)
        {
            var offset = await GetOffset(response.FileName, workspace);

            response.Line -= offset;

            return response;
        }

        public static async Task<NavigateResponse> TranslateAsync(this NavigateResponse response, OmniSharpWorkspace workspace, Request request)
        {
            var offset = await GetOffset(request.FileName, workspace);

            response.Line -= offset;

            return response;
        }

        public static async Task<FileMemberTree> TranslateAsync(this FileMemberTree response, OmniSharpWorkspace workspace, Request request)
        {
            var offsets = new Dictionary<string, int>();

            foreach (var topLevelTypeDefinition in response.TopLevelTypeDefinitions)
            {
                await topLevelTypeDefinition.TranslateAsync(workspace, request, offsets);
            }

            return response;
        }

        private static async Task<FileMemberElement> TranslateAsync(this FileMemberElement element, OmniSharpWorkspace workspace, Request request, IDictionary<string, int> offsets)
        {
            element.Location = await element.Location.TranslateAsync(workspace, request, offsets);

            foreach (var childNode in element.ChildNodes)
            {
                await childNode.TranslateAsync(workspace, request, offsets);
            }

            return element;
        }

        private static async Task<QuickFix> TranslateAsync(this QuickFix quickFix, OmniSharpWorkspace workspace, Request request, IDictionary<string, int> offsets)
        {
            var fileName = !string.IsNullOrEmpty(quickFix.FileName) ? quickFix.FileName : request.FileName;

            if (string.IsNullOrEmpty(fileName))
            {
                return quickFix;
            }

            if (!offsets.ContainsKey(fileName))
            {
                offsets[fileName] = await GetOffset(fileName, workspace);
            }

            quickFix.Line -= offsets[fileName];
            quickFix.EndLine -= offsets[fileName];

            return quickFix;
        }

        private static async Task<int> GetOffset(string fileName, OmniSharpWorkspace workspace)
        {
            return await LineOffsetHelper.GetOffset(fileName, workspace) + 1;
        }
    }
}
