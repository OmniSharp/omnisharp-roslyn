using Microsoft.CodeAnalysis.CodeActions;

namespace OmniSharp.Roslyn.CSharp.Extensions
{
    public static class CodeActionExtensions
    {
        public static string GetIdentifier(this CodeAction codeAction)
        {
            return codeAction.EquivalenceKey ?? codeAction.Title;
        }
    }
}
