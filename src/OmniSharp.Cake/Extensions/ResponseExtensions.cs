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
            var quickFixes = new List<QuickFix>();

            foreach (var quickFix in response.QuickFixes)
            {
                await quickFix.TranslateAsync(workspace, request);

                if (quickFix.Line >= 0)
                {
                    quickFixes.Add(quickFix);
                }
            }

            response.QuickFixes = quickFixes;
            return response;
        }

        public static async Task<NavigateResponse> TranslateAsync(this NavigateResponse response, OmniSharpWorkspace workspace, Request request)
        {
            var (line, _) = await LineIndexHelper.TranslateFromGenerated(request.FileName, response.Line, workspace, true);

            response.Line = line;

            return response;
        }

        public static async Task<FileMemberTree> TranslateAsync(this FileMemberTree response, OmniSharpWorkspace workspace, Request request)
        {
            var zeroIndex = await LineIndexHelper.TranslateToGenerated(request.FileName, 0, workspace);
            var topLevelTypeDefinitions = new List<FileMemberElement>();

            foreach (var topLevelTypeDefinition in response.TopLevelTypeDefinitions)
            {
                if (topLevelTypeDefinition.Location.Line < zeroIndex)
                {
                    continue;
                }

                await topLevelTypeDefinition.TranslateAsync(workspace, request);

                if (topLevelTypeDefinition.Location.Line >= 0)
                {
                    topLevelTypeDefinitions.Add(topLevelTypeDefinition);
                }
            }

            response.TopLevelTypeDefinitions = topLevelTypeDefinitions;
            return response;
        }

        private static async Task<FileMemberElement> TranslateAsync(this FileMemberElement element, OmniSharpWorkspace workspace, Request request)
        {
            element.Location = await element.Location.TranslateAsync(workspace, request);
            var childNodes = new List<FileMemberElement>();

            foreach (var childNode in element.ChildNodes)
            {
                await childNode.TranslateAsync(workspace, request);

                if (childNode.Location.Line >= 0)
                {
                    childNodes.Add(childNode);
                }
            }

            element.ChildNodes = childNodes;
            return element;
        }

        private static async Task<QuickFix> TranslateAsync(this QuickFix quickFix, OmniSharpWorkspace workspace, Request request)
        {
            var sameFile = string.IsNullOrEmpty(quickFix.FileName);
            var fileName = !sameFile ? quickFix.FileName : request.FileName;

            if (string.IsNullOrEmpty(fileName))
            {
                return quickFix;
            }

            var (line, newFileName) = await LineIndexHelper.TranslateFromGenerated(fileName, quickFix.Line, workspace, sameFile);

            quickFix.Line = line;
            quickFix.FileName = newFileName;

            (line, _) = await LineIndexHelper.TranslateFromGenerated(fileName, quickFix.EndLine, workspace, sameFile);

            quickFix.EndLine = line;

            return quickFix;
        }
    }
}
