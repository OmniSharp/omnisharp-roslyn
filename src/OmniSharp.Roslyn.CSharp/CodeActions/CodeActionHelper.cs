using System;
using System.Collections.Generic;
using System.Composition;
using Microsoft.CodeAnalysis.CodeFixes;
using Microsoft.CodeAnalysis.CodeRefactorings;
using OmniSharp.Services;

namespace OmniSharp.Roslyn.CSharp.Services.CodeActions
{
    /// <summary>
    /// This class contains code fixes and refactorings that should be removed for various reasons.
    /// </summary>
    [Export, Shared]
    public class CodeActionHelper
    {
        public const string AddImportProviderName = "Microsoft.CodeAnalysis.CSharp.AddImport.CSharpAddImportCodeFixProvider";
        public const string RemoveUnnecessaryUsingsProviderName = "Microsoft.CodeAnalysis.CSharp.RemoveUnnecessaryImports.CSharpRemoveUnnecessaryImportsCodeFixProvider";

        private static readonly HashSet<string> _roslynListToRemove = new HashSet<string>
        {
            "Microsoft.CodeAnalysis.CSharp.AddMissingReference.CSharpAddMissingReferenceCodeFixProvider",
            "Microsoft.CodeAnalysis.CSharp.CodeFixes.Async.CSharpConvertToAsyncMethodCodeFixProvider",
            "Microsoft.CodeAnalysis.CSharp.CodeFixes.Iterator.CSharpChangeToIEnumerableCodeFixProvider",
            "Microsoft.CodeAnalysis.ChangeSignature.ChangeSignatureCodeRefactoringProvider",
            "Microsoft.CodeAnalysis.ExtractInterface.ExtractInterfaceCodeRefactoringProvider"
        };

        private static readonly HashSet<string> _nrefactoryListToRemove = new HashSet<string>
        {
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

        [ImportingConstructor]
        public CodeActionHelper(IAssemblyLoader loader)
        {
            // Check to see if the Roslyn code fix and refactoring provider type names can be found.
            // If this fails, OmniSharp has updated to a new version of Roslyn and one of the type names changed.
            var csharpFeatureAssembly = loader.Load(Configuration.RoslynCSharpFeatures);
            var featureAssembly = loader.Load(Configuration.RoslynFeatures);
            var workspaceAssembly = loader.Load(Configuration.RoslynWorkspaces);

            foreach (var typeName in _roslynListToRemove)
            {
                if (csharpFeatureAssembly.GetType(typeName) == null &&
                    featureAssembly.GetType(typeName) == null &&
                    workspaceAssembly.GetType(typeName) == null)
                {
                    throw new InvalidOperationException($"Could not find '{typeName}'. Has this type name changed?");
                }
            }
        }

        public bool IsDisallowed(string typeName)
        {
            return _roslynListToRemove.Contains(typeName)
                || _nrefactoryListToRemove.Contains(typeName);
        }

        public bool IsDisallowed(CodeFixProvider provider)
        {
            return IsDisallowed(provider.GetType().FullName);
        }

        public bool IsDisallowed(CodeRefactoringProvider provider)
        {
            return IsDisallowed(provider.GetType().FullName);
        }
    }
}
