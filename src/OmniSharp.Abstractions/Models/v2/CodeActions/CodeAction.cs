namespace OmniSharp.Models.V2.CodeActions
{
    public class OmniSharpCodeAction
    {
        public OmniSharpCodeAction(string identifier, string name)
        {
            Identifier = identifier;
            Name = name;
        }

        public string Identifier { get; }
        public string Name { get; }
    }
}
