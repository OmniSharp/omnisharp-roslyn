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
using OmniSharp.Extensions;
using OmniSharp.Mef;
using OmniSharp.Models.V2.CodeActions;
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

        protected Lazy<List<CodeFixProvider>> OrderedCodeFixProviders;
        protected Lazy<List<CodeRefactoringProvider>> OrderedCodeRefactoringProviders;

        protected BaseCodeActionService(OmniSharpWorkspace workspace, CodeActionHelper helper, IEnumerable<ICodeActionProvider> providers, ILogger logger)
        {
            this.Workspace = workspace;
            this.Providers = providers;
            this.Logger = logger;
            this._helper = helper;

            OrderedCodeFixProviders = new Lazy<List<CodeFixProvider>>(() => GetSortedCodeFixProviders());
            OrderedCodeRefactoringProviders = new Lazy<List<CodeRefactoringProvider>>(() => GetSortedCodeRefactoringProviders());

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
                return sourceText.GetSpanFromRange(request.Selection);
            }

            var position = sourceText.GetPositionFromLineAndOffset(request.Line, request.Column);
            return new TextSpan(position, length: 0);
        }

        private async Task CollectCodeFixesActions(Document document, TextSpan span, List<CodeAction> codeActions)
        {
            var semanticModel = await document.GetSemanticModelAsync();

            var groupedBySpan = semanticModel.GetDiagnostics()
                .Where(diagnostic => span.IntersectsWith(diagnostic.Location.SourceSpan))
                .GroupBy(diagnostic => diagnostic.Location.SourceSpan);

            foreach (var diagnosticGroupedBySpan in groupedBySpan)
            {
                var diagnosticSpan = diagnosticGroupedBySpan.Key;
                var diagnosticsWithSameSpan = diagnosticGroupedBySpan.OrderByDescending(d => d.Severity);

                await AppendFixesAsync(document, diagnosticSpan, diagnosticsWithSameSpan, codeActions);
            }
        }

        private async Task AppendFixesAsync(Document document, TextSpan span, IEnumerable<Diagnostic> diagnostics, List<CodeAction> codeActions)
        {
            foreach (var codeFixProvider in OrderedCodeFixProviders.Value)
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

        private List<CodeFixProvider> GetSortedCodeFixProviders()
        {
            var codeFixProviders = this.Providers
                .SelectMany(provider => provider.CodeFixProviders)
                .ToList();

            return SortByTopologyIfPossibleOrReturnAsItWas(codeFixProviders);
        }

        private List<CodeRefactoringProvider> GetSortedCodeRefactoringProviders()
        {
            var codeRefactoringProviders = this.Providers
                .SelectMany(provider => provider.CodeRefactoringProviders)
                .ToList();

            return SortByTopologyIfPossibleOrReturnAsItWas(codeRefactoringProviders);
        }

        private List<T> SortByTopologyIfPossibleOrReturnAsItWas<T>(IEnumerable<T> source)
        {
            var codeFixNodes = source.Select(codeFix => ProviderNode<T>.From(codeFix)).ToList();

            var graph = Graph<T>.GetGraph(codeFixNodes);

            if (graph.HasCycles())
            {
                return source.ToList();
            }

            return graph.TopologicalSort();
        }

        private bool HasFix(CodeFixProvider codeFixProvider, string diagnosticId)
        {
            return !_helper.IsDisallowed(codeFixProvider.GetType().FullName);
        }

        private async Task CollectRefactoringActions(Document document, TextSpan span, List<CodeAction> codeActions)
        {
            var availableRefactorings = OrderedCodeRefactoringProviders.Value.Where(x => !_helper.IsDisallowed(x));

            foreach (var codeRefactoringProvider in availableRefactorings)
            {
                try
                {
                    var context = new CodeRefactoringContext(document, span, a => codeActions.Add(a), CancellationToken.None);
                    await codeRefactoringProvider.ComputeRefactoringsAsync(context);
                }
                catch (Exception ex)
                {
                    this.Logger.LogError(ex, $"Error computing refactorings for {codeRefactoringProvider.GetType().FullName}");
                }
            }
        }

        private IEnumerable<AvailableCodeAction> ConvertToAvailableCodeAction(IEnumerable<CodeAction> actions)
        {
            return actions.SelectMany(action =>
            {
                var nestedActions = this._getNestedCodeActions.Invoke<ImmutableArray<CodeAction>>(action, null);

                if (nestedActions.Any())
                {
                    return nestedActions.Select(nestedAction => new AvailableCodeAction(nestedAction, action));
                }

                return new[] { new AvailableCodeAction(action) };
            });
        }
    }
}
