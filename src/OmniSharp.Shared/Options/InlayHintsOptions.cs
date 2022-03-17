namespace OmniSharp.Options
{
    public struct InlayHintsOptions
    {
        public static readonly InlayHintsOptions AllOn = new()
        {
            EnableForParameters = true,
            ForLiteralParameters = true,
            ForIndexerParameters = true,
            ForObjectCreationParameters = true,
            ForOtherParameters = true,
            SuppressForParametersThatDifferOnlyBySuffix = true,
            SuppressForParametersThatMatchMethodIntent = true,
            SuppressForParametersThatMatchArgumentName = true,
            EnableForTypes = true,
            ForImplicitVariableTypes = true,
            ForLambdaParameterTypes = true,
            ForImplicitObjectCreation = true
        };

        public bool EnableForParameters { get; set; }
        public bool ForLiteralParameters { get; set; }
        public bool ForIndexerParameters { get; set; }
        public bool ForObjectCreationParameters { get; set; }
        public bool ForOtherParameters { get; set; }
        public bool SuppressForParametersThatDifferOnlyBySuffix { get; set; }
        public bool SuppressForParametersThatMatchMethodIntent { get; set; }
        public bool SuppressForParametersThatMatchArgumentName { get; set; }

        public bool EnableForTypes { get; set; }
        public bool ForImplicitVariableTypes { get; set; }
        public bool ForLambdaParameterTypes { get; set; }
        public bool ForImplicitObjectCreation { get; set; }
    }
}
