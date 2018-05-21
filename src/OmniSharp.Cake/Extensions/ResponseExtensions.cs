using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using OmniSharp.Cake.Utilities;
using OmniSharp.Models;
using OmniSharp.Models.Navigate;
using OmniSharp.Models.MembersTree;
using OmniSharp.Models.Rename;

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

        public static async Task<RenameResponse> TranslateAsync(this RenameResponse response, OmniSharpWorkspace workspace, RenameRequest request)
        {
            var changes = new Dictionary<string, List<LinePositionSpanTextChange>>();

            foreach (var change in response.Changes)
            {
                await PopulateModificationsAsync(change, workspace, changes);
            }

            response.Changes = changes.Select(x => new ModifiedFileResponse(x.Key)
            {
                Changes = x.Value
            });

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

        private static async Task PopulateModificationsAsync(
            ModifiedFileResponse modification,
            OmniSharpWorkspace workspace,
            IDictionary<string, List<LinePositionSpanTextChange>> modifications)
        {
            foreach (var change in modification.Changes)
            {
                var (filename, _) = await change.TranslateAsync(workspace, modification.FileName);

                if (change.StartLine < 0)
                {
                    continue;
                }

                if (modifications.TryGetValue(filename, out var changes))
                {
                    changes.Add(change);
                }
                else
                {
                    modifications.Add(filename, new List<LinePositionSpanTextChange>
                    {
                        change
                    });
                }
            }
        }

        private static async Task<(string, LinePositionSpanTextChange)> TranslateAsync(this LinePositionSpanTextChange change, OmniSharpWorkspace workspace, string fileName)
        {
            var (line, newFileName) = await LineIndexHelper.TranslateFromGenerated(fileName, change.StartLine, workspace, false);

            change.StartLine = line;

            (line, _) = await LineIndexHelper.TranslateFromGenerated(fileName, change.EndLine, workspace, false);

            change.EndLine = line;

            return (newFileName, change);
        }
    }
}
