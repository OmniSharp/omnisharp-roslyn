using System.Collections.Immutable;

namespace OmniSharp.MSBuild
{
    internal static class Errors
    {
        public static readonly ImmutableHashSet<string> NullableWarnings;

        // sourced from https://github.com/dotnet/roslyn/blob/v4.2.0-3.22151.16/src/Compilers/CSharp/Portable/Errors/ErrorFacts.cs#L27-L82 
        static Errors()
        {
            ImmutableHashSet<string>.Builder nullableWarnings = ImmutableHashSet.CreateBuilder<string>();

            nullableWarnings.Add("CS8601"); // WRN_NullReferenceAssignment
            nullableWarnings.Add("CS8602"); // WRN_NullReferenceReceiver
            nullableWarnings.Add("CS8603"); // WRN_NullReferenceReturn
            nullableWarnings.Add("CS8604"); // WRN_NullReferenceArgument

            nullableWarnings.Add("CS8618"); // WRN_UninitializedNonNullableField
            nullableWarnings.Add("CS8619"); // WRN_NullabilityMismatchInAssignment
            nullableWarnings.Add("CS8620"); // WRN_NullabilityMismatchInArgument
            nullableWarnings.Add("CS8624"); // WRN_NullabilityMismatchInArgumentForOutput

            nullableWarnings.Add("CS8621"); // WRN_NullabilityMismatchInReturnTypeOfTargetDelegate
            nullableWarnings.Add("CS8622"); // WRN_NullabilityMismatchInParameterTypeOfTargetDelegate
            nullableWarnings.Add("CS8625"); // WRN_NullAsNonNullable
            nullableWarnings.Add("CS8629"); // WRN_NullableValueTypeMayBeNull
            nullableWarnings.Add("CS8631"); // WRN_NullabilityMismatchInTypeParameterConstraint
            nullableWarnings.Add("CS8634"); // WRN_NullabilityMismatchInTypeParameterReferenceTypeConstraint
            nullableWarnings.Add("CS8714"); // WRN_NullabilityMismatchInTypeParameterNotNullConstraint

            nullableWarnings.Add("CS8597"); // WRN_ThrowPossibleNull
            nullableWarnings.Add("CS8605"); // WRN_UnboxPossibleNull
            nullableWarnings.Add("CS8655"); // WRN_SwitchExpressionNotExhaustiveForNull
            nullableWarnings.Add("CS8847"); // WRN_SwitchExpressionNotExhaustiveForNullWithWhen

            nullableWarnings.Add("CS8600"); // WRN_ConvertingNullableToNonNullable
            nullableWarnings.Add("CS8607"); // WRN_DisallowNullAttributeForbidsMaybeNullAssignment
            nullableWarnings.Add("CS8762"); // WRN_ParameterConditionallyDisallowsNull
            nullableWarnings.Add("CS8763"); // WRN_ShouldNotReturn


            nullableWarnings.Add("CS8608"); // WRN_NullabilityMismatchInTypeOnOverride
            nullableWarnings.Add("CS8609"); // WRN_NullabilityMismatchInReturnTypeOnOverride
            nullableWarnings.Add("CS8819"); // WRN_NullabilityMismatchInReturnTypeOnPartial
            nullableWarnings.Add("CS8610"); // WRN_NullabilityMismatchInParameterTypeOnOverride
            nullableWarnings.Add("CS8611"); // WRN_NullabilityMismatchInParameterTypeOnPartial
            nullableWarnings.Add("CS8612"); // WRN_NullabilityMismatchInTypeOnImplicitImplementation
            nullableWarnings.Add("CS8613"); // WRN_NullabilityMismatchInReturnTypeOnImplicitImplementation
            nullableWarnings.Add("CS8614"); // WRN_NullabilityMismatchInParameterTypeOnImplicitImplementation
            nullableWarnings.Add("CS8615"); // WRN_NullabilityMismatchInTypeOnExplicitImplementation
            nullableWarnings.Add("CS8616"); // WRN_NullabilityMismatchInReturnTypeOnExplicitImplementation
            nullableWarnings.Add("CS8617"); // WRN_NullabilityMismatchInParameterTypeOnExplicitImplementation
            nullableWarnings.Add("CS8633"); // WRN_NullabilityMismatchInConstraintsOnImplicitImplementation
            nullableWarnings.Add("CS8643"); // WRN_NullabilityMismatchInExplicitlyImplementedInterface
            nullableWarnings.Add("CS8644"); // WRN_NullabilityMismatchInInterfaceImplementedByBase
            nullableWarnings.Add("CS8645"); // WRN_DuplicateInterfaceWithNullabilityMismatchInBaseList
            nullableWarnings.Add("CS8667"); // WRN_NullabilityMismatchInConstraintsOnPartialImplementation
            nullableWarnings.Add("CS8670"); // WRN_NullReferenceInitializer
            nullableWarnings.Add("CS8770"); // WRN_DoesNotReturnMismatch
            nullableWarnings.Add("CS8769"); // WRN_TopLevelNullabilityMismatchInParameterTypeOnExplicitImplementation
            nullableWarnings.Add("CS8767"); // WRN_TopLevelNullabilityMismatchInParameterTypeOnImplicitImplementation
            nullableWarnings.Add("CS8765"); // WRN_TopLevelNullabilityMismatchInParameterTypeOnOverride
            nullableWarnings.Add("CS8768"); // WRN_TopLevelNullabilityMismatchInReturnTypeOnExplicitImplementation
            nullableWarnings.Add("CS8766"); // WRN_TopLevelNullabilityMismatchInReturnTypeOnImplicitImplementation
            nullableWarnings.Add("CS8764"); // WRN_TopLevelNullabilityMismatchInReturnTypeOnOverride
            nullableWarnings.Add("CS8774"); // WRN_MemberNotNull
            nullableWarnings.Add("CS8776"); // WRN_MemberNotNullBadMember
            nullableWarnings.Add("CS8775"); // WRN_MemberNotNullWhen
            nullableWarnings.Add("CS8777"); // WRN_ParameterDisallowsNull
            nullableWarnings.Add("CS8824"); // WRN_ParameterNotNullIfNotNull
            nullableWarnings.Add("CS8825"); // WRN_ReturnNotNullIfNotNull

            NullableWarnings = nullableWarnings.ToImmutable();
        }
    }
}
