namespace OmniSharp.Models.TypeLookup
{
    public class TypeLookupResponse
    {
        public string Type { get; set; }
        public string Documentation { get; set; }
        public DocumentationComment StructuredDocumentation { get; set; }
    }
}
