using Microsoft.CodeAnalysis.ExternalAccess.OmniSharp.CodeActions;
using Microsoft.CodeAnalysis.ExternalAccess.OmniSharp.ImplementType;
using OmniSharp.Options;

namespace OmniSharp.Roslyn.CodeActions
{
    internal static class CodeActionOptionsFactory
    {
        public static OmniSharpCodeActionOptions Create(OmniSharpOptions options)
            => new OmniSharpCodeActionOptions(
                new OmniSharpImplementTypeOptions(
                    (OmniSharpImplementTypeInsertionBehavior)options.ImplementTypeOptions.InsertionBehavior,
                    (OmniSharpImplementTypePropertyGenerationBehavior)options.ImplementTypeOptions.PropertyGenerationBehavior),
                OmniSharpLineFormattingOptionsProvider.CreateFromOptions(options));
    }
}
