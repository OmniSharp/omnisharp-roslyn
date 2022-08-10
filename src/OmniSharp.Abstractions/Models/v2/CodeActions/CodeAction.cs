namespace OmniSharp.Models.V2.CodeActions
{
    public class OmniSharpCodeAction
    {
        public OmniSharpCodeAction(string identifier, string name, bool isCodeFix)
        {
            Identifier = identifier;
            Name = name;
            IsCodeFix = isCodeFix;
        }

        public string Identifier { get; }
        public string Name { get; }
        public bool IsCodeFix { get; }
    }
}
