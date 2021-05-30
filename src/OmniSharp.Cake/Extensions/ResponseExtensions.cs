using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using OmniSharp.Cake.Utilities;
using OmniSharp.Models;
using OmniSharp.Models.Navigate;
using OmniSharp.Models.MembersTree;
using OmniSharp.Models.Rename;
using OmniSharp.Models.v1.Completion;
using OmniSharp.Models.V2;
using OmniSharp.Models.V2.CodeActions;
using OmniSharp.Models.V2.CodeStructure;
using OmniSharp.Utilities;

namespace OmniSharp.Cake.Extensions
{
    internal static class ResponseExtensions
    {
        public static QuickFixResponse OnlyThisFile(this QuickFixResponse response, string fileName)
        {
            if (response?.QuickFixes == null)
            {
                return response;
            }

            var quickFixes = response.QuickFixes.Where(x => PathsAreEqual(x.FileName, fileName));
            response.QuickFixes = quickFixes;
            return response;
        }

        public static Task<QuickFixResponse> TranslateAsync(this QuickFixResponse response, OmniSharpWorkspace workspace)
        {
            return response.TranslateAsync(workspace, new Request());
        }

        public static async Task<QuickFixResponse> TranslateAsync(this QuickFixResponse response, OmniSharpWorkspace workspace, Request request, bool removeGenerated = false)
        {
            var quickFixes = new List<QuickFix>();

            foreach (var quickFix in response.QuickFixes)
            {
                await quickFix.TranslateAsync(workspace, request);

                if (quickFix.Line < 0)
                {
                    continue;
                }
                if (removeGenerated && quickFix.FileName.Equals(Constants.Paths.Generated))
                {
                    continue;
                }
                quickFixes.Add(quickFix);
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

        public static async Task<RunCodeActionResponse> TranslateAsync(this RunCodeActionResponse response,
            OmniSharpWorkspace workspace)
        {
            if (response?.Changes == null)
            {
                return response;
            }

            var fileOperations = new List<FileOperationResponse>();
            var changes = new Dictionary<string, List<LinePositionSpanTextChange>>();

            foreach (var fileOperation in response.Changes)
            {
                if (fileOperation.ModificationType == FileModificationType.Modified &&
                    fileOperation is ModifiedFileResponse modifiedFile)
                {
                    await PopulateModificationsAsync(modifiedFile, workspace, changes);
                }

                fileOperations.Add(fileOperation);
            }

            foreach (var change in changes)
            {

                if (fileOperations.FirstOrDefault(x => x.FileName == change.Key &&
                                                       x.ModificationType == FileModificationType.Modified)
                    is ModifiedFileResponse modifiedFile)
                {
                    modifiedFile.Changes = change.Value;
                }
            }

            response.Changes = fileOperations;
            return response;
        }

        public static async Task<CodeStructureResponse> TranslateAsync(this CodeStructureResponse response, OmniSharpWorkspace workspace, CodeStructureRequest request)
        {
            var zeroIndex = await LineIndexHelper.TranslateToGenerated(request.FileName, 0, workspace);
            var elements = new List<CodeElement>();

            foreach (var element in response.Elements)
            {
                if (element.Ranges.Values.Any(x => x.Start.Line < zeroIndex))
                {
                    continue;
                }

                var translatedElement = await element.TranslateAsync(workspace, request);

                if (translatedElement.Ranges.Values.Any(x => x.Start.Line < 0))
                {
                    continue;
                }

                elements.Add(translatedElement);
            }

            response.Elements = elements;
            return response;
        }

        public static async Task<BlockStructureResponse> TranslateAsync(this BlockStructureResponse response, OmniSharpWorkspace workspace, SimpleFileRequest request)
        {
            if (response?.Spans == null)
            {
                return response;
            }

            var spans = new List<CodeFoldingBlock>();

            foreach (var span in response.Spans)
            {
                var range = await span.Range.TranslateAsync(workspace, request);

                if (range.Start.Line < 0)
                {
                    continue;
                }

                spans.Add(new CodeFoldingBlock(range, span.Kind));
            }

            response.Spans = spans;
            return response;
        }

        public static async Task<CompletionResponse> TranslateAsync(this CompletionResponse response, OmniSharpWorkspace workspace, CompletionRequest request)
        {
            foreach (var item in response.Items)
            {
                if (item.TextEdit is null)
                {
                    continue;
                }

                var (_, textEdit) = await item.TextEdit.TranslateAsync(workspace, request.FileName);
                item.TextEdit = textEdit;

                List<LinePositionSpanTextChange> additionalTextEdits = null;

                foreach (var additionalTextEdit in item.AdditionalTextEdits ?? Enumerable.Empty<LinePositionSpanTextChange>())
                {
                    var (_, change) = await additionalTextEdit.TranslateAsync(workspace, request.FileName);

                    // Due to the fact that AdditionalTextEdits return the complete buffer, we can't currently use that in Cake.
                    // Revisit when we have a solution. At this point it's probably just best to remove AdditionalTextEdits.
                    if (change.StartLine < 0)
                    {
                        continue;
                    }

                    additionalTextEdits ??= new List<LinePositionSpanTextChange>();
                    additionalTextEdits.Add(change);
                }

                item.AdditionalTextEdits = additionalTextEdits;
            }

            return response;
        }

        private static async Task<CodeElement> TranslateAsync(this CodeElement element, OmniSharpWorkspace workspace, SimpleFileRequest request)
        {
            var builder = new CodeElement.Builder
            {
                Kind = element.Kind,
                DisplayName = element.DisplayName,
                Name = element.Name
            };

            foreach (var property in element.Properties ?? Enumerable.Empty<KeyValuePair<string, object>>())
            {
                builder.AddProperty(property.Key, property.Value);
            }

            foreach (var range in element.Ranges ?? Enumerable.Empty<KeyValuePair<string, Range>>())
            {
                builder.AddRange(range.Key, await range.Value.TranslateAsync(workspace, request));
            }

            foreach (var childElement in element.Children ?? Enumerable.Empty<CodeElement>())
            {
                var translatedElement = await childElement.TranslateAsync(workspace, request);

                // This is plain stupid, but someone might put a #load directive inside a method or class
                if (translatedElement.Ranges.Values.Any(x => x.Start.Line < 0))
                {
                    continue;
                }

                builder.AddChild(translatedElement);
            }

            return builder.ToCodeElement();
        }

        private static async Task<Range> TranslateAsync(this Range range, OmniSharpWorkspace workspace, SimpleFileRequest request)
        {
            var (line, _) = await LineIndexHelper.TranslateFromGenerated(request.FileName, range.Start.Line, workspace, true);

            if (range.Start.Line == range.End.Line)
            {
                return range with
                {
                    Start = range.Start with { Line = line },
                    End = range.End with { Line = line }
                };
            }

            var (endLine, _) = await LineIndexHelper.TranslateFromGenerated(request.FileName, range.End.Line, workspace, true);
            return range with
            {
                Start = range.Start with { Line = line },
                End = range.End with { Line = endLine }
            };
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

        private static bool PathsAreEqual(string x, string y)
        {
            if (x == null && y == null)
            {
                return true;
            }
            if (x == null || y == null)
            {
                return false;
            }

            var comparer = PlatformHelper.IsWindows ? StringComparison.OrdinalIgnoreCase : StringComparison.Ordinal;

            return Path.GetFullPath(x).Equals(Path.GetFullPath(y), comparer);
        }
    }
}
