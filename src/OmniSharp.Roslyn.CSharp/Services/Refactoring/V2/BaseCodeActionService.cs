using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CodeActions;
using Microsoft.CodeAnalysis.CodeFixes;
using Microsoft.CodeAnalysis.CodeRefactorings;
using Microsoft.CodeAnalysis.Text;
using Microsoft.Extensions.Logging;
using OmniSharp.Models.V2;
using OmniSharp.Services;

namespace OmniSharp.Roslyn.CSharp.Services.CodeActions
{
    public abstract class BaseCodeActionService<TRequest, TResponse> : RequestHandler<TRequest, TResponse>
    {
        protected readonly OmniSharpWorkspace Workspace;
        protected readonly IEnumerable<ICodeActionProvider> Providers;
        protected readonly ILogger Logger;

        private readonly CodeActionHelper _helper;

        protected BaseCodeActionService(OmniSharpWorkspace workspace, CodeActionHelper helper, IEnumerable<ICodeActionProvider> providers, ILogger logger)
        {
            this.Workspace = workspace;
            this.Providers = providers;
            this.Logger = logger;
            this._helper = helper;
        }

        public abstract Task<TResponse> Handle(TRequest request);


        protected async Task<IEnumerable<CodeAction>> GetActionsAsync(ICodeActionRequest request)
        {
            var actions = new List<CodeAction>();
            var originalDocument = this.Workspace.GetDocument(request.FileName);
            if (originalDocument == null)
            {
                return actions;
            }

            var refactoringContext = await GetRefactoringContext(originalDocument, request, actions);
            if (refactoringContext != null)
            {
                await CollectRefactoringActions(refactoringContext.Value);
            }

            var codeFixContext = await GetCodeFixContext(originalDocument, request, actions);
            if (codeFixContext != null)
            {
                await CollectCodeFixActions(codeFixContext.Value);
            }

            actions.Reverse();

            return actions;
        }

        private async Task<CodeRefactoringContext?> GetRefactoringContext(Document originalDocument, ICodeActionRequest request, List<CodeAction> actionsDestination)
        {
            var sourceText = await originalDocument.GetTextAsync();
            var location = GetTextSpan(request, sourceText);
            return new CodeRefactoringContext(originalDocument, location, (a) => actionsDestination.Add(a), CancellationToken.None);
        }

        private async Task<CodeFixContext?> GetCodeFixContext(Document originalDocument, ICodeActionRequest request, List<CodeAction> actionsDestination)
        {
            var sourceText = await originalDocument.GetTextAsync();
            var semanticModel = await originalDocument.GetSemanticModelAsync();
            var diagnostics = semanticModel.GetDiagnostics();
            var span = GetTextSpan(request, sourceText);

            // Try to find exact match
            var pointDiagnostics = diagnostics.Where(d => d.Location.SourceSpan.Equals(span)).ToImmutableArray();
            // No exact match found, try approximate match instead
            if (pointDiagnostics.Length == 0)
            {
                var firstMatchingDiagnostic = diagnostics.FirstOrDefault(d => d.Location.SourceSpan.Contains(span));
                // Try to find other matches with the same exact span as the first approximate match
                if (firstMatchingDiagnostic != null)
                {
                    pointDiagnostics = diagnostics.Where(d => d.Location.SourceSpan.Equals(firstMatchingDiagnostic.Location.SourceSpan)).ToImmutableArray();
                }
            }
            // At this point all pointDiagnostics guaranteed to have the same span
            if (pointDiagnostics.Length > 0)
            {
                return new CodeFixContext(originalDocument, pointDiagnostics[0].Location.SourceSpan, pointDiagnostics, (a, d) => actionsDestination.Add(a), CancellationToken.None);
            }

            return null;
        }

        private TextSpan GetTextSpan(ICodeActionRequest request, SourceText sourceText)
        {
            if (request.Selection != null)
            {
                var startPosition = sourceText.Lines.GetPosition(new LinePosition(request.Selection.Start.Line, request.Selection.Start.Column));
                var endPosition = sourceText.Lines.GetPosition(new LinePosition(request.Selection.End.Line, request.Selection.End.Column));
                return TextSpan.FromBounds(startPosition, endPosition);
            }

            var position = sourceText.Lines.GetPosition(new LinePosition(request.Line, request.Column));
            return new TextSpan(position, 1);
        }

        private async Task CollectCodeFixActions(CodeFixContext context)
        {
            var diagnosticIds = context.Diagnostics.Select(d => d.Id).ToArray();

            foreach (var provider in this.Providers)
            {
                foreach (var codeFix in provider.CodeFixes)
                {
                    if (_helper.IsDisallowed(codeFix))
                    {
                        continue;
                    }

                    // TODO: This is a horrible hack! However, remove unnecessary usings only
                    // responds for diagnostics that are produced by its diagnostic analyzer.
                    // We need to provide a *real* diagnostic engine to address this.
                    if (codeFix.ToString() != CodeActionHelper.RemoveUnnecessaryUsingsProviderName)
                    {
                        if (!diagnosticIds.Any(id => codeFix.FixableDiagnosticIds.Contains(id)))
                        {
                            continue;
                        }
                    }

                    try
                    {
                        await codeFix.RegisterCodeFixesAsync(context);
                    }
                    catch (Exception ex)
                    {
                        this.Logger.LogError(ex, $"Error registering code fixes for {codeFix.GetType().FullName}");
                    }
                }
            }
        }

        private async Task CollectRefactoringActions(CodeRefactoringContext context)
        {
            foreach (var provider in this.Providers)
            {
                foreach (var refactoring in provider.Refactorings)
                {
                    if (_helper.IsDisallowed(refactoring))
                    {
                        continue;
                    }

                    try
                    {
                        await refactoring.ComputeRefactoringsAsync(context);
                    }
                    catch (Exception ex)
                    {
                        this.Logger.LogError(ex, $"Error computing refactorings for {refactoring.GetType().FullName}");
                    }
                }
            }
        }
    }
}
