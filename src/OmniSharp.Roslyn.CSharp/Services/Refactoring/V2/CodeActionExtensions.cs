using Microsoft.CodeAnalysis.CodeActions;

namespace OmniSharp.Api.V2
{
    public static class CodeActionExtensions
    {
        public static string GetIdentifier(this CodeAction codeAction)
        {
            return codeAction.EquivalenceKey ?? codeAction.Title;
        }
    }
}
