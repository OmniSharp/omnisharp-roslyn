using System;
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

        private static readonly Func<TextSpan, List<Diagnostic>> s_createDiagnosticList = _ => new List<Diagnostic>();

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
            var document = this.Workspace.GetDocument(request.FileName);
            if (document == null)
            {
                return Array.Empty<AvailableCodeAction>();
            }

            var codeActions = new List<CodeAction>();

            var sourceText = await document.GetTextAsync();
            var span = GetTextSpan(request, sourceText);

            await CollectCodeFixesActions(document, span, codeActions);
            await CollectRefactoringActions(document, span, codeActions);

            // TODO: Determine good way to order code actions.
            codeActions.Reverse();

            // Be sure to filter out any code actions that inherit from CodeActionWithOptions.
            // This isn't a great solution and might need changing later, but every Roslyn code action
            // derived from this type tries to display a dialog. For now, this is a reasonable solution.
            var availableActions = ConvertToAvailableCodeAction(codeActions)
                .Where(a => !a.CodeAction.GetType().GetTypeInfo().IsSubclassOf(typeof(CodeActionWithOptions)));

            return availableActions;
        }

        private TextSpan GetTextSpan(ICodeActionRequest request, SourceText sourceText)
        {
            if (request.Selection != null)
            {
                return TextSpan.FromBounds(
                    sourceText.Lines.GetPosition(new LinePosition(request.Selection.Start.Line, request.Selection.Start.Column)),
                    sourceText.Lines.GetPosition(new LinePosition(request.Selection.End.Line, request.Selection.End.Column)));
            }

            var position = sourceText.Lines.GetPosition(new LinePosition(request.Line, request.Column));
            return new TextSpan(position, length: 0);
        }

        private async Task CollectCodeFixesActions(Document document, TextSpan span, List<CodeAction> codeActions)
        {
            Dictionary<TextSpan, List<Diagnostic>> aggregatedDiagnostics = null;

            var semanticModel = await document.GetSemanticModelAsync();

            foreach (var diagnostic in semanticModel.GetDiagnostics())
            {
                if (!span.IntersectsWith(diagnostic.Location.SourceSpan))
                {
                    continue;
                }

                aggregatedDiagnostics = aggregatedDiagnostics ?? new Dictionary<TextSpan, List<Diagnostic>>();
                var list = aggregatedDiagnostics.GetOrAdd(diagnostic.Location.SourceSpan, s_createDiagnosticList);
                list.Add(diagnostic);
            }

            if (aggregatedDiagnostics == null)
            {
                return;
            }

            foreach (var kvp in aggregatedDiagnostics)
            {
                var diagnosticSpan = kvp.Key;
                var diagnosticsWithSameSpan = kvp.Value.OrderByDescending(d => d.Severity);

                await AppendFixesAsync(document, diagnosticSpan, diagnosticsWithSameSpan, codeActions);
            }
        }

        private async Task AppendFixesAsync(Document document, TextSpan span, IEnumerable<Diagnostic> diagnostics, List<CodeAction> codeActions)
        {
            foreach (var provider in this.Providers)
            {
                foreach (var codeFixProvider in provider.CodeFixProviders)
                {
                    var fixableDiagnostics = diagnostics.Where(d => HasFix(codeFixProvider, d.Id)).ToImmutableArray();
                    if (fixableDiagnostics.Length > 0)
                    {
                        var context = new CodeFixContext(document, span, fixableDiagnostics, (a, _) => codeActions.Add(a), CancellationToken.None);

                        try
                        {
                            await codeFixProvider.RegisterCodeFixesAsync(context);
                        }
                        catch (Exception ex)
                        {
                            this.Logger.LogError(ex, $"Error registering code fixes for {codeFixProvider.GetType().FullName}");
                        }
                    }
                }
            }
        }

        private bool HasFix(CodeFixProvider codeFixProvider, string diagnosticId)
        {
            var typeName = codeFixProvider.GetType().FullName;

            if (_helper.IsDisallowed(typeName))
            {
                return false;
            }

            // TODO: This is a horrible hack! However, remove unnecessary usings only
            // responds for diagnostics that are produced by its diagnostic analyzer.
            // We need to provide a *real* diagnostic engine to address this.
            if (typeName != CodeActionHelper.RemoveUnnecessaryUsingsProviderName)
            {
                if (!codeFixProvider.FixableDiagnosticIds.Contains(diagnosticId))
                {
                    return false;
                }
            }
            else if (diagnosticId != "CS8019") // ErrorCode.HDN_UnusedUsingDirective
            {
                return false;
            }

            return true;
        }

        private async Task CollectRefactoringActions(Document document, TextSpan span, List<CodeAction> codeActions)
        {
            foreach (var provider in this.Providers)
            {
                foreach (var codeRefactoringProvider in provider.CodeRefactoringProviders)
                {
                    if (_helper.IsDisallowed(codeRefactoringProvider))
                    {
                        continue;
                    }

                    var context = new CodeRefactoringContext(document, span, a => codeActions.Add(a), CancellationToken.None);

                    try
                    {
                        await codeRefactoringProvider.ComputeRefactoringsAsync(context);
                    }
                    catch (Exception ex)
                    {
                        this.Logger.LogError(ex, $"Error computing refactorings for {codeRefactoringProvider.GetType().FullName}");
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
