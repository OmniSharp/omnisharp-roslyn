using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Composition;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CodeActions;
using Microsoft.CodeAnalysis.CodeFixes;
using Microsoft.CodeAnalysis.CodeRefactorings;
using Microsoft.CodeAnalysis.Text;
using Microsoft.Framework.Logging;
using OmniSharp.Mef;
using OmniSharp.Models.V2;
using OmniSharp.Roslyn.CSharp.Extensions;
using OmniSharp.Services;

namespace OmniSharp.Roslyn.CSharp.Services.Refactoring.V2
{
    public static class CodeActionHelper
    {
        public static async Task<IEnumerable<CodeAction>> GetActions(OmnisharpWorkspace workspace, IEnumerable<ICodeActionProvider> codeActionProviders, ILogger logger, ICodeActionRequest request)
        {
            var actions = new List<CodeAction>();
            var originalDocument = workspace.GetDocument(request.FileName);
            if (originalDocument == null)
            {
                return actions;
            }

            var refactoringContext = await GetRefactoringContext(originalDocument, request, actions);
            var codeFixContext = await GetCodeFixContext(originalDocument, request, actions);
            await CollectRefactoringActions(codeActionProviders, logger, refactoringContext);
            await CollectCodeFixActions(codeActionProviders, logger, codeFixContext);
            actions.Reverse();
            return actions;
        }

        private static async Task<CodeRefactoringContext?> GetRefactoringContext(Document originalDocument, ICodeActionRequest request, List<CodeAction> actionsDestination)
        {
            var sourceText = await originalDocument.GetTextAsync();
            var location = GetTextSpan(request, sourceText);
            return new CodeRefactoringContext(originalDocument, location, (a) => actionsDestination.Add(a), CancellationToken.None);
        }

        private static async Task<CodeFixContext?> GetCodeFixContext(Document originalDocument, ICodeActionRequest request, List<CodeAction> actionsDestination)
        {
            var sourceText = await originalDocument.GetTextAsync();
            var semanticModel = await originalDocument.GetSemanticModelAsync();
            var diagnostics = semanticModel.GetDiagnostics();
            var location = GetTextSpan(request, sourceText);

            var pointDiagnostics = diagnostics.Where(d => d.Location.SourceSpan.Contains(location)).ToImmutableArray();
            if (pointDiagnostics.Any())
            {
                return new CodeFixContext(originalDocument, pointDiagnostics.First().Location.SourceSpan, pointDiagnostics, (a, d) => actionsDestination.Add(a), CancellationToken.None);
            }

            return null;
        }

        private static TextSpan GetTextSpan(ICodeActionRequest request, SourceText sourceText)
        {
            if (request.Selection != null)
            {
                var startPosition = sourceText.Lines.GetPosition(new LinePosition(request.Selection.Start.Line - 1, request.Selection.Start.Column - 1));
                var endPosition = sourceText.Lines.GetPosition(new LinePosition(request.Selection.End.Line - 1, request.Selection.End.Column - 1));
                return TextSpan.FromBounds(startPosition, endPosition);
            }
            var position = sourceText.Lines.GetPosition(new LinePosition(request.Line - 1, request.Column - 1));
            return new TextSpan(position, 1);
        }

        private static readonly HashSet<string> _blacklist = new HashSet<string> {
            // This list is horrible but will be temporary
            "Microsoft.CodeAnalysis.CSharp.CodeFixes.AddMissingReference.AddMissingReferenceCodeFixProvider",
            "Microsoft.CodeAnalysis.CSharp.CodeFixes.Async.CSharpConvertToAsyncMethodCodeFixProvider",
            "Microsoft.CodeAnalysis.CSharp.CodeFixes.Iterator.CSharpChangeToIEnumerableCodeFixProvider",
            "Microsoft.CodeAnalysis.CSharp.CodeRefactorings.ChangeSignature.ChangeSignatureCodeRefactoringProvider"
        };

        private static async Task CollectCodeFixActions(IEnumerable<ICodeActionProvider> codeActionProviders, ILogger logger, CodeFixContext? fixContext)
        {
            if (!fixContext.HasValue)
                return;

            foreach (var provider in codeActionProviders)
            {
                foreach (var codeFix in provider.CodeFixes)
                {
                    if (_blacklist.Contains(codeFix.ToString()))
                    {
                        continue;
                    }

                    try
                    {
                        await codeFix.RegisterCodeFixesAsync(fixContext.Value);
                    }
                    catch
                    {
                        logger.LogError("Error registering code fixes " + codeFix);
                    }
                }
            }
        }

        private static async Task CollectRefactoringActions(IEnumerable<ICodeActionProvider> codeActionProviders, ILogger logger, CodeRefactoringContext? refactoringContext)
        {
            if (!refactoringContext.HasValue)
                return;

            foreach (var provider in codeActionProviders)
            {
                foreach (var refactoring in provider.Refactorings)
                {
                    if (_blacklist.Contains(refactoring.ToString()))
                    {
                        continue;
                    }

                    try
                    {
                        await refactoring.ComputeRefactoringsAsync(refactoringContext.Value);
                    }
                    catch (Exception e)
                    {
                        logger.LogWarning("Problem computing refactorings for " + refactoring, e);
                    }
                }
            }
        }
    }
}
