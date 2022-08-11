using System.Diagnostics;

namespace OmniSharp.Models.V2.CodeActions
{
    public class OmniSharpCodeAction
    {
        public OmniSharpCodeAction(string identifier, string name, string codeActionKind)
        {
            Debug.Assert(codeActionKind is CodeActions.CodeActionKind.QuickFix
                                        or CodeActions.CodeActionKind.Refactor
                                        or CodeActions.CodeActionKind.RefactorExtract
                                        or CodeActions.CodeActionKind.RefactorInline);
            Identifier = identifier;
            Name = name;
            CodeActionKind = codeActionKind;
        }

        public string Identifier { get; }
        public string Name { get; }
        public string CodeActionKind { get; }
    }
}
