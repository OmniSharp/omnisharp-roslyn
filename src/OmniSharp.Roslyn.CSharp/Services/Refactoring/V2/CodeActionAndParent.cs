using System;
using Microsoft.CodeAnalysis.CodeActions;

namespace OmniSharp.Roslyn.CSharp.Services.Refactoring.V2
{
    public class CodeActionAndParent
    {
        public CodeAction CodeAction { get; }
        public CodeAction ParentCodeAction { get; }

        public CodeActionAndParent(CodeAction codeAction, CodeAction parentCodeAction = null)
        {
            if (codeAction == null)
            {
                throw new ArgumentNullException(nameof(codeAction));
            }

            this.CodeAction = codeAction;
            this.ParentCodeAction = parentCodeAction;
        }

        public string GetTitle()
        {
            return ParentCodeAction != null
                ? $"{ParentCodeAction.Title} -> {CodeAction.Title}"
                : CodeAction.Title;
        }
    }
}
