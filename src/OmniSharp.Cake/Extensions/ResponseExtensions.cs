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
            foreach (var quickFix in response.QuickFixes)
            {
                await quickFix.TranslateAsync(workspace, request);
            }

            return response;
        }

        public static async Task<GotoDefinitionResponse> TranslateAsync(this GotoDefinitionResponse response, OmniSharpWorkspace workspace)
        {
            var offset = await LineOffsetHelper.GetOffset(response.FileName, response.Line, workspace);

            response.Line -= offset;

            return response;
        }

        public static async Task<NavigateResponse> TranslateAsync(this NavigateResponse response, OmniSharpWorkspace workspace, Request request)
        {
            var offset = await LineOffsetHelper.GetOffset(request.FileName, response.Line, workspace);

            response.Line -= offset;

            return response;
        }

        public static async Task<FileMemberTree> TranslateAsync(this FileMemberTree response, OmniSharpWorkspace workspace, Request request)
        {
            foreach (var topLevelTypeDefinition in response.TopLevelTypeDefinitions)
            {
                await topLevelTypeDefinition.TranslateAsync(workspace, request);
            }

            return response;
        }

        private static async Task<FileMemberElement> TranslateAsync(this FileMemberElement element, OmniSharpWorkspace workspace, Request request)
        {
            element.Location = await element.Location.TranslateAsync(workspace, request);

            foreach (var childNode in element.ChildNodes)
            {
                await childNode.TranslateAsync(workspace, request);
            }

            return element;
        }

        private static async Task<QuickFix> TranslateAsync(this QuickFix quickFix, OmniSharpWorkspace workspace, Request request)
        {
            var fileName = !string.IsNullOrEmpty(quickFix.FileName) ? quickFix.FileName : request.FileName;

            if (string.IsNullOrEmpty(fileName))
            {
                return quickFix;
            }

            var offset = await LineOffsetHelper.GetOffset(fileName, quickFix.Line, workspace);

            quickFix.Line -= offset;
            quickFix.EndLine -= offset;

            return quickFix;
        }
    }
}
