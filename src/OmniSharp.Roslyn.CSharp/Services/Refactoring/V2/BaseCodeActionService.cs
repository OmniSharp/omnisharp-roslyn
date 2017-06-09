﻿using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CodeActions;
using Microsoft.CodeAnalysis.CodeFixes;
using Microsoft.CodeAnalysis.CodeRefactorings;
using Microsoft.CodeAnalysis.Text;
using Microsoft.Extensions.Logging;
using OmniSharp.Mef;
using OmniSharp.Models.V2;
using OmniSharp.Roslyn.CSharp.Services.CodeActions;
using OmniSharp.Services;
using OmniSharp.Utilities;

namespace OmniSharp.Roslyn.CSharp.Services.Refactoring.V2
{
    public abstract class BaseCodeActionService<TRequest, TResponse> : IRequestHandler<TRequest, TResponse>
    {
        protected readonly OmniSharpWorkspace Workspace;
        protected readonly IEnumerable<ICodeActionProvider> Providers;
        protected readonly ILogger Logger;

        private readonly CodeActionHelper _helper;

        private readonly MethodInfo _getNestedCodeActions;

        protected BaseCodeActionService(OmniSharpWorkspace workspace, CodeActionHelper helper, IEnumerable<ICodeActionProvider> providers, ILogger logger)
        {
            this.Workspace = workspace;
            this.Providers = providers;
            this.Logger = logger;
            this._helper = helper;

            // Sadly, the CodeAction.NestedCodeActions property is still internal.
            var nestedCodeActionsProperty = typeof(CodeAction).GetProperty("NestedCodeActions", BindingFlags.NonPublic | BindingFlags.Instance);
            if (nestedCodeActionsProperty == null)
            {
                throw new InvalidOperationException("Could not find CodeAction.NestedCodeActions property.");
            }

            this._getNestedCodeActions = nestedCodeActionsProperty.GetGetMethod(nonPublic: true);
            if (this._getNestedCodeActions == null)
            {
                throw new InvalidOperationException("Could not retrieve 'get' method for CodeAction.NestedCodeActions property.");
            }
        }

        public abstract Task<TResponse> Handle(TRequest request);

        protected async Task<IEnumerable<AvailableCodeAction>> GetAvailableCodeActions(ICodeActionRequest request)
        {
            var originalDocument = this.Workspace.GetDocument(request.FileName);
            if (originalDocument == null)
            {
                return Array.Empty<AvailableCodeAction>();
            }

            var actions = new List<CodeAction>();

            var codeFixContext = await GetCodeFixContext(originalDocument, request, actions);
            if (codeFixContext != null)
            {
                await CollectCodeFixActions(codeFixContext.Value);
            }

            var refactoringContext = await GetRefactoringContext(originalDocument, request, actions);
            if (refactoringContext != null)
            {
                await CollectRefactoringActions(refactoringContext.Value);
            }

            // TODO: Determine good way to order code actions.
            actions.Reverse();

            // Be sure to filter out any code actions that inherit from CodeActionWithOptions.
            // This isn't a great solution and might need changing later, but every Roslyn code action
            // derived from this type tries to display a dialog. For now, this is a reasonable solution.
            var availableActions = ConvertToAvailableCodeAction(actions)
                .Where(a => !a.CodeAction.GetType().GetTypeInfo().IsSubclassOf(typeof(CodeActionWithOptions)));

            return availableActions;
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

                    var typeName = codeFix.GetType().FullName;

                    // TODO: This is a horrible hack! However, remove unnecessary usings only
                    // responds for diagnostics that are produced by its diagnostic analyzer.
                    // We need to provide a *real* diagnostic engine to address this.
                    if (typeName != CodeActionHelper.RemoveUnnecessaryUsingsProviderName)
                    {
                        if (!diagnosticIds.Any(id => codeFix.FixableDiagnosticIds.Contains(id)))
                        {
                            continue;
                        }
                    }

                    if (typeName == CodeActionHelper.RemoveUnnecessaryUsingsProviderName &&
                        !diagnosticIds.Contains("CS8019")) // ErrorCode.HDN_UnusedUsingDirective
                    {
                        continue;
                    }

                    try
                    {
                        await codeFix.RegisterCodeFixesAsync(context);
                    }
                    catch (Exception ex)
                    {
                        this.Logger.LogError(ex, $"Error registering code fixes for {typeName}");
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

        private IEnumerable<AvailableCodeAction> ConvertToAvailableCodeAction(IEnumerable<CodeAction> actions)
        {
            var codeActions = new List<AvailableCodeAction>();

            foreach (var action in actions)
            {
                var handledNestedActions = false;

                // Roslyn supports "nested" code actions in order to allow submenus in the VS light bulb menu.
                // For now, we'll just expand nested code actions in place.
                var nestedActions = this._getNestedCodeActions.Invoke<ImmutableArray<CodeAction>>(action, null);
                if (nestedActions.Length > 0)
                {
                    foreach (var nestedAction in nestedActions)
                    {
                        codeActions.Add(new AvailableCodeAction(nestedAction, action));
                    }

                    handledNestedActions = true;
                }

                if (!handledNestedActions)
                {
                    codeActions.Add(new AvailableCodeAction(action));
                }
            }

            return codeActions;
        }
    }
}
