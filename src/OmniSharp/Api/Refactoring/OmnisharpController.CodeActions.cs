using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNet.Mvc;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CodeActions;
using Microsoft.CodeAnalysis.CodeFixes;
using Microsoft.CodeAnalysis.CodeRefactorings;
using Microsoft.CodeAnalysis.Text;
using OmniSharp.Models;
using OmniSharp.Services;

namespace OmniSharp
{
    public class CodeActionController
    {
        private readonly OmnisharpWorkspace _workspace;
        private readonly IEnumerable<ICodeActionProvider> _codeActionProviders;
        private Document _originalDocument;

        public CodeActionController(OmnisharpWorkspace workspace, IEnumerable<ICodeActionProvider> providers)
        {
            _workspace = workspace;
            _codeActionProviders = providers;
        }

        [HttpPost("getcodeactions")]
        public async Task<GetCodeActionsResponse> GetCodeActions(CodeActionRequest request)
        {
            var actions = await GetActions(request);
            return new GetCodeActionsResponse() { CodeActions = actions.Select(a => a.Title) };
        }

        [HttpPost("runcodeaction")]
        public async Task<RunCodeActionResponse> RunCodeAction(CodeActionRequest request)
        {
            var actions = await GetActions(request);

            if (request.CodeAction > actions.Count())
            {
                return new RunCodeActionResponse();
            }

            var action = actions.ElementAt(request.CodeAction);

            var operations = await action.GetOperationsAsync(CancellationToken.None);

            var solution = _workspace.CurrentSolution;
            foreach (var o in operations)
            {
                o.Apply(_workspace, CancellationToken.None);
            }

            var response = new RunCodeActionResponse();
            if (!request.WantsTextChanges)
            {
                // return the new document
                var sourceText = await _workspace.CurrentSolution.GetDocument(_originalDocument.Id).GetTextAsync();
                response.Text = sourceText.ToString();
            }
            else
            {
                // return the text changes
                var changes = await FileChanges.GetFileChangesAsync(solution, _workspace.CurrentSolution, true);
                response.Changes = changes;
            }
            _workspace.TryApplyChanges(_workspace.CurrentSolution);
            return response;
        }

        private async Task<IEnumerable<CodeAction>> GetActions(CodeActionRequest request)
        {
            var actions = new List<CodeAction>();
            _originalDocument = _workspace.GetDocument(request.FileName);
            if (_originalDocument == null)
                return actions;

            var refactoringContext = await GetRefactoringContext(request, actions);
            var codeFixContext = await GetCodeFixContext(request, actions);
            await CollectRefactoringActions(refactoringContext);
            await CollectCodeFixActions(codeFixContext);
            actions.Reverse();
            return actions;
        }

        private async Task<CodeRefactoringContext?> GetRefactoringContext(CodeActionRequest request, List<CodeAction> actionsDestination)
        {
            var sourceText = await _originalDocument.GetTextAsync();
            var location = GetTextSpan(request, sourceText);

            return new CodeRefactoringContext(_originalDocument, location, (a) => actionsDestination.Add(a), CancellationToken.None);
        }

        private async Task<CodeFixContext?> GetCodeFixContext(CodeActionRequest request, List<CodeAction> actionsDestination)
        {
            var sourceText = await _originalDocument.GetTextAsync();
            var semanticModel = await _originalDocument.GetSemanticModelAsync();
            var diagnostics = semanticModel.GetDiagnostics();
            var location = GetTextSpan(request, sourceText);

            var pointDiagnostics = diagnostics.Where(d => d.Location.SourceSpan.Contains(location)).ToImmutableArray();
            if (pointDiagnostics.Any())
                return new CodeFixContext(_originalDocument, pointDiagnostics.First().Location.SourceSpan, pointDiagnostics, (a, d) => actionsDestination.Add(a), CancellationToken.None);
            return null;
        }

        private static TextSpan GetTextSpan(CodeActionRequest request, SourceText sourceText)
        {
            if (request.HasRange)
            {
                var startPosition = sourceText.Lines.GetPosition(new LinePosition(request.SelectionStartLine.Value - 1, request.SelectionStartColumn.Value - 1));
                var endPosition = sourceText.Lines.GetPosition(new LinePosition(request.SelectionEndLine.Value - 1, request.SelectionEndColumn.Value - 1));
                return TextSpan.FromBounds(startPosition, endPosition);
            }

            var position = sourceText.Lines.GetPosition(new LinePosition(request.Line - 1, request.Column - 1));
            return new TextSpan(position, 1);
        }

        private static readonly HashSet<string> _blacklist = new HashSet<string> {

            "Microsoft.CodeAnalysis.CSharp.CodeFixes.AddMissingReference.AddMissingReferenceCodeFixProvider",
            "Microsoft.CodeAnalysis.CSharp.CodeFixes.Async.CSharpConvertToAsyncMethodCodeFixProvider",
            "Microsoft.CodeAnalysis.CSharp.CodeFixes.Iterator.CSharpChangeToIEnumerableCodeFixProvider",
            "ICSharpCode.NRefactory6.CSharp.Refactoring.CreateMethodDeclarationAction",
            "ICSharpCode.NRefactory6.CSharp.Refactoring.UseStringFormatAction",
            "ICSharpCode.NRefactory6.CSharp.Refactoring.UseAsAndNullCheckAction",
            "ICSharpCode.NRefactory6.CSharp.Refactoring.SplitStringAction",
            "ICSharpCode.NRefactory6.CSharp.Refactoring.SplitIfAction",
            "ICSharpCode.NRefactory6.CSharp.Refactoring.SplitDeclarationListAction",
            "ICSharpCode.NRefactory6.CSharp.Refactoring.SplitDeclarationAndAssignmentAction",
            "ICSharpCode.NRefactory6.CSharp.Refactoring.SimplifyIfInLoopsFlowAction",
            "ICSharpCode.NRefactory6.CSharp.Refactoring.SimplifyIfFlowAction",
            "ICSharpCode.NRefactory6.CSharp.Refactoring.ReverseDirectionForForLoopAction",
            "ICSharpCode.NRefactory6.CSharp.Refactoring.ReplaceWithOperatorAssignmentAction",
            "ICSharpCode.NRefactory6.CSharp.Refactoring.RedundantCaseLabelFixProvider",
            "ICSharpCode.NRefactory6.CSharp.Refactoring.LocalVariableNotUsedFixProvider",
            "ICSharpCode.NRefactory6.CSharp.Refactoring.PartialTypeWithSinglePartFixProvider",
            "ICSharpCode.NRefactory6.CSharp.Refactoring.RedundantBaseConstructorCallFixProvider",
            "ICSharpCode.NRefactory6.CSharp.Refactoring.RedundantDefaultFieldInitializerFixProvider",
            "ICSharpCode.NRefactory6.CSharp.Refactoring.RedundantOverridenMemberFixProvider",
            "ICSharpCode.NRefactory6.CSharp.Refactoring.RedundantParamsFixProvider",
            "ICSharpCode.NRefactory6.CSharp.Refactoring.UnusedLabelFixProvider",
            "ICSharpCode.NRefactory6.CSharp.Refactoring.UnusedParameterFixProvider",
            "ICSharpCode.NRefactory6.CSharp.Refactoring.ConstantNullCoalescingConditionFixProvider",
            "ICSharpCode.NRefactory6.CSharp.Refactoring.RedundantArgumentDefaultValueFixProvider",
            "ICSharpCode.NRefactory6.CSharp.Refactoring.RedundantBoolCompareFixProvider",
            "ICSharpCode.NRefactory6.CSharp.Refactoring.RedundantCatchClauseFixProvider",
            "ICSharpCode.NRefactory6.CSharp.Refactoring.RedundantCheckBeforeAssignmentFixProvider",
            "ICSharpCode.NRefactory6.CSharp.Refactoring.RedundantCommaInArrayInitializerFixProvider",
            "ICSharpCode.NRefactory6.CSharp.Refactoring.RedundantComparisonWithNullFixProvider",
            "ICSharpCode.NRefactory6.CSharp.Refactoring.RedundantDelegateCreationFixProvider",
            "ICSharpCode.NRefactory6.CSharp.Refactoring.RedundantEmptyFinallyBlockFixProvider",
            "ICSharpCode.NRefactory6.CSharp.Refactoring.RedundantEnumerableCastCallFixProvider",
            "ICSharpCode.NRefactory6.CSharp.Refactoring.RedundantObjectCreationArgumentListFixProvider",
            "ICSharpCode.NRefactory6.CSharp.Refactoring.RedundantExplicitArrayCreationFixProvider",
            "ICSharpCode.NRefactory6.CSharp.Refactoring.RedundantExplicitArraySizeFixProvider",
            "ICSharpCode.NRefactory6.CSharp.Refactoring.RedundantExplicitNullableCreationFixProvider",
            "ICSharpCode.NRefactory6.CSharp.Refactoring.RedundantExtendsListEntryFixProvider",
            "ICSharpCode.NRefactory6.CSharp.Refactoring.RedundantIfElseBlockFixProvider",
            "ICSharpCode.NRefactory6.CSharp.Refactoring.RedundantLambdaParameterTypeFixProvider",
            "ICSharpCode.NRefactory6.CSharp.Refactoring.RedundantLambdaSignatureParenthesesFixProvider",
            "ICSharpCode.NRefactory6.CSharp.Refactoring.RedundantLogicalConditionalExpressionOperandFixProvider",
            "ICSharpCode.NRefactory6.CSharp.Refactoring.RedundantObjectOrCollectionInitializerFixProvider",
            "ICSharpCode.NRefactory6.CSharp.Refactoring.RedundantStringToCharArrayCallFixProvider",
            "ICSharpCode.NRefactory6.CSharp.Refactoring.RedundantToStringCallForValueTypesFixProvider",
            "ICSharpCode.NRefactory6.CSharp.Refactoring.RedundantToStringCallFixProvider",
            "ICSharpCode.NRefactory6.CSharp.Refactoring.RedundantUnsafeContextFixProvider",
            "ICSharpCode.NRefactory6.CSharp.Refactoring.RedundantUsingDirectiveFixProvider",
            "ICSharpCode.NRefactory6.CSharp.Refactoring.RemoveRedundantOrStatementFixProvider",
            "ICSharpCode.NRefactory6.CSharp.Refactoring.UnusedAnonymousMethodSignatureFixProvider",
            "ICSharpCode.NRefactory6.CSharp.Refactoring.AccessToStaticMemberViaDerivedTypeFixProvider",
            "ICSharpCode.NRefactory6.CSharp.Refactoring.ConvertIfToOrExpressionFixProvider",
            "ICSharpCode.NRefactory6.CSharp.Refactoring.ConvertToConstantFixProvider",
            "ICSharpCode.NRefactory6.CSharp.Refactoring.MemberCanBeMadeStaticFixProvider",
            "ICSharpCode.NRefactory6.CSharp.Refactoring.ParameterCanBeDeclaredWithBaseTypeFixProvider",
            "ICSharpCode.NRefactory6.CSharp.Refactoring.PossibleMistakenCallToGetTypeFixProvider",
            "ICSharpCode.NRefactory6.CSharp.Refactoring.PublicConstructorInAbstractClassFixProvider",
            "ICSharpCode.NRefactory6.CSharp.Refactoring.ReferenceEqualsWithValueTypeFixProvider",
            "ICSharpCode.NRefactory6.CSharp.Refactoring.ReplaceWithFirstOrDefaultFixProvider",
            "ICSharpCode.NRefactory6.CSharp.Refactoring.ReplaceWithLastOrDefaultFixProvider",
            "ICSharpCode.NRefactory6.CSharp.Refactoring.ReplaceWithOfTypeAnyFixProvider",
            "ICSharpCode.NRefactory6.CSharp.Refactoring.ReplaceWithOfTypeCountFixProvider",
            "ICSharpCode.NRefactory6.CSharp.Refactoring.ReplaceWithOfTypeFirstFixProvider",
            "ICSharpCode.NRefactory6.CSharp.Refactoring.ReplaceWithOfTypeFirstOrDefaultFixProvider",
            "ICSharpCode.NRefactory6.CSharp.Refactoring.ReplaceWithOfTypeFixProvider",
            "ICSharpCode.NRefactory6.CSharp.Refactoring.ReplaceWithOfTypeLastFixProvider",
            "ICSharpCode.NRefactory6.CSharp.Refactoring.ReplaceWithOfTypeLastOrDefaultFixProvider",
            "ICSharpCode.NRefactory6.CSharp.Refactoring.ReplaceWithOfTypeLongCountFixProvider",
            "ICSharpCode.NRefactory6.CSharp.Refactoring.ReplaceWithOfTypeSingleFixProvider",
            "ICSharpCode.NRefactory6.CSharp.Refactoring.ReplaceWithOfTypeSingleOrDefaultFixProvider",
            "ICSharpCode.NRefactory6.CSharp.Refactoring.ReplaceWithOfTypeWhereFixProvider",
            "ICSharpCode.NRefactory6.CSharp.Refactoring.ReplaceWithSingleCallToAnyFixProvider",
            "ICSharpCode.NRefactory6.CSharp.Refactoring.ReplaceWithSingleCallToCountFixProvider",
            "ICSharpCode.NRefactory6.CSharp.Refactoring.ReplaceWithSingleCallToFirstFixProvider",
            "ICSharpCode.NRefactory6.CSharp.Refactoring.ReplaceWithSingleCallToFirstOrDefaultFixProvider",
            "ICSharpCode.NRefactory6.CSharp.Refactoring.ReplaceWithSingleCallToLastFixProvider",
            "ICSharpCode.NRefactory6.CSharp.Refactoring.ReplaceWithSingleCallToLastOrDefaultFixProvider",
            "ICSharpCode.NRefactory6.CSharp.Refactoring.ReplaceWithSingleCallToLongCountFixProvider",
            "ICSharpCode.NRefactory6.CSharp.Refactoring.ReplaceWithSingleCallToSingleFixProvider",
            "ICSharpCode.NRefactory6.CSharp.Refactoring.ReplaceWithSingleCallToSingleOrDefaultFixProvider",
            "ICSharpCode.NRefactory6.CSharp.Refactoring.ReplaceWithStringIsNullOrEmptyFixProvider",
            "ICSharpCode.NRefactory6.CSharp.Refactoring.SimplifyConditionalTernaryExpressionFixProvider",
            "ICSharpCode.NRefactory6.CSharp.Refactoring.SimplifyLinqExpressionFixProvider",
            "ICSharpCode.NRefactory6.CSharp.Refactoring.StringCompareIsCultureSpecificFixProvider",
            "ICSharpCode.NRefactory6.CSharp.Refactoring.StringCompareToIsCultureSpecificFixProvider",
            "ICSharpCode.NRefactory6.CSharp.Refactoring.StringEndsWithIsCultureSpecificFixProvider",
            "ICSharpCode.NRefactory6.CSharp.Refactoring.StringLastIndexOfIsCultureSpecificFixProvider",
            "ICSharpCode.NRefactory6.CSharp.Refactoring.StringIndexOfIsCultureSpecificFixProvider",
            "ICSharpCode.NRefactory6.CSharp.Refactoring.StringStartsWithIsCultureSpecificFixProvider",
            "ICSharpCode.NRefactory6.CSharp.Refactoring.UseArrayCreationExpressionFixProvider",
            "ICSharpCode.NRefactory6.CSharp.Refactoring.UseIsOperatorFixProvider",
            "ICSharpCode.NRefactory6.CSharp.Refactoring.UseMethodAnyFixProvider",
            "ICSharpCode.NRefactory6.CSharp.Refactoring.UseMethodIsInstanceOfTypeFixProvider",
            "ICSharpCode.NRefactory6.CSharp.Refactoring.ConvertIfStatementToNullCoalescingExpressionFixProvider",
            "ICSharpCode.NRefactory6.CSharp.Refactoring.ConvertIfStatementToSwitchStatementFixProvider",
            "ICSharpCode.NRefactory6.CSharp.Refactoring.ConvertNullableToShortFormFixProvider",
            "ICSharpCode.NRefactory6.CSharp.Refactoring.ConvertToAutoPropertyFixProvider",
            "ICSharpCode.NRefactory6.CSharp.Refactoring.ConvertToLambdaExpressionFixProvider",
            "ICSharpCode.NRefactory6.CSharp.Refactoring.ForCanBeConvertedToForeachFixProvider",
            "ICSharpCode.NRefactory6.CSharp.Refactoring.RewriteIfReturnToReturnFixProvider",
            "ICSharpCode.NRefactory6.CSharp.Refactoring.SuggestUseVarKeywordEvidentFixProvider",
            "ICSharpCode.NRefactory6.CSharp.Refactoring.CS0183ExpressionIsAlwaysOfProvidedTypeFixProvider",
            "ICSharpCode.NRefactory6.CSharp.Refactoring.CS1573ParameterHasNoMatchingParamTagFixProvider",
            "ICSharpCode.NRefactory6.CSharp.Refactoring.CS1717AssignmentMadeToSameVariableFixProvider",
            "ICSharpCode.NRefactory6.CSharp.Refactoring.UnassignedReadonlyFieldFixProvider",
            "ICSharpCode.NRefactory6.CSharp.Refactoring.ProhibitedModifiersFixProvider",
            "ICSharpCode.NRefactory6.CSharp.Refactoring.CanBeReplacedWithTryCastAndCheckForNullFixProvider",
            "ICSharpCode.NRefactory6.CSharp.Refactoring.EqualExpressionComparisonFixProvider",
            "ICSharpCode.NRefactory6.CSharp.Refactoring.ForControlVariableIsNeverModifiedFixProvider",
            "ICSharpCode.NRefactory6.CSharp.Refactoring.FormatStringProblemFixProvider",
            "ICSharpCode.NRefactory6.CSharp.Refactoring.FunctionNeverReturnsFixProvider",
            "ICSharpCode.NRefactory6.CSharp.Refactoring.LocalVariableHidesMemberFixProvider",
            "ICSharpCode.NRefactory6.CSharp.Refactoring.LongLiteralEndingLowerLFixProvider",
            "ICSharpCode.NRefactory6.CSharp.Refactoring.MemberHidesStaticFromOuterClassFixProvider",
            "ICSharpCode.NRefactory6.CSharp.Refactoring.MethodOverloadWithOptionalParameterFixProvider",
            "ICSharpCode.NRefactory6.CSharp.Refactoring.NotResolvedInTextIssueFixProvider",
            "ICSharpCode.NRefactory6.CSharp.Refactoring.OperatorIsCanBeUsedIssueFixProvider",
            "ICSharpCode.NRefactory6.CSharp.Refactoring.OptionalParameterHierarchyMismatchFixProvider",
            "ICSharpCode.NRefactory6.CSharp.Refactoring.ParameterHidesMemberFixProvider",
            "ICSharpCode.NRefactory6.CSharp.Refactoring.PartialMethodParameterNameMismatchFixProvider",
            "ICSharpCode.NRefactory6.CSharp.Refactoring.PolymorphicFieldLikeEventInvocationFixProvider",
            "ICSharpCode.NRefactory6.CSharp.Refactoring.PossibleAssignmentToReadonlyFieldFixProvider",
            "ICSharpCode.NRefactory6.CSharp.Refactoring.PossibleMultipleEnumerationFixProvider",
            "ICSharpCode.NRefactory6.CSharp.Refactoring.StaticFieldInGenericTypeFixProvider",
            "ICSharpCode.NRefactory6.CSharp.Refactoring.ThreadStaticAtInstanceFieldFixProvider",
            "ICSharpCode.NRefactory6.CSharp.Refactoring.ValueParameterNotUsedFixProvider",
            "ICSharpCode.NRefactory6.CSharp.Refactoring.AdditionalOfTypeFixProvider",
            "ICSharpCode.NRefactory6.CSharp.Refactoring.CastExpressionOfIncompatibleTypeFixProvider",
            "ICSharpCode.NRefactory6.CSharp.Refactoring.CheckNamespaceFixProvider",
            "ICSharpCode.NRefactory6.CSharp.Refactoring.ConstantConditionFixProvider",
            "ICSharpCode.NRefactory6.CSharp.Refactoring.ConvertIfToAndExpressionFixProvider",
            "ICSharpCode.NRefactory6.CSharp.Refactoring.LockThisFixProvider",
            "ICSharpCode.NRefactory6.CSharp.Refactoring.NegativeRelationalExpressionFixProvider",
            "ICSharpCode.NRefactory6.CSharp.Refactoring.ParameterOnlyAssignedFixProvider",
            "ICSharpCode.NRefactory6.CSharp.Refactoring.RedundantAssignmentFixProvider",
            "ICSharpCode.NRefactory6.CSharp.Refactoring.StaticEventSubscriptionFixProvider",
            "ICSharpCode.NRefactory6.CSharp.Refactoring.XmlDocFixProvider",
            "ICSharpCode.NRefactory6.CSharp.Refactoring.CS0029InvalidConversionFixProvider",
            "ICSharpCode.NRefactory6.CSharp.Refactoring.CS0126ReturnMustBeFollowedByAnyExpressionFixProvider",
            "ICSharpCode.NRefactory6.CSharp.Refactoring.CS0127ReturnMustNotBeFollowedByAnyExpressionFixProvider",
            "ICSharpCode.NRefactory6.CSharp.Refactoring.CS0152DuplicateCaseLabelValueFixProvider",
            "ICSharpCode.NRefactory6.CSharp.Refactoring.CS0169FieldIsNeverUsedFixProvider",
            "ICSharpCode.NRefactory6.CSharp.Refactoring.CS0618UsageOfObsoleteMemberFixProvider",
            "ICSharpCode.NRefactory6.CSharp.Refactoring.CS0659ClassOverrideEqualsWithoutGetHashCodeFixProvider",
            "ICSharpCode.NRefactory6.CSharp.Refactoring.CS0759RedundantPartialMethodIssueFixProvider",
            "ICSharpCode.NRefactory6.CSharp.Refactoring.CS1729TypeHasNoConstructorWithNArgumentsFixProvider",
            "ICSharpCode.NRefactory6.CSharp.Refactoring.ExpressionIsNeverOfProvidedTypeFixProvider",
            "ICSharpCode.NRefactory6.CSharp.Refactoring.MissingInterfaceMemberImplementationFixProvider",
            "ICSharpCode.NRefactory6.CSharp.Refactoring.InconsistentNamingIssueFixProvider",
            "ICSharpCode.NRefactory6.CSharp.Refactoring.ConditionIsAlwaysTrueOrFalseFixProvider"
        };

        private async Task CollectCodeFixActions(CodeFixContext? fixContext)
        {
            if (!fixContext.HasValue)
                return;

            foreach (var provider in _codeActionProviders)
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
                        System.Console.WriteLine("error " + codeFix);
                    }
                }
            }
        }

        private async Task CollectRefactoringActions(CodeRefactoringContext? refactoringContext)
        {
            if (!refactoringContext.HasValue)
                return;

            foreach (var provider in _codeActionProviders)
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
                    catch
                    {
                        System.Console.WriteLine("error " + refactoring);
                    }
                }
            }
        }
    }
}
