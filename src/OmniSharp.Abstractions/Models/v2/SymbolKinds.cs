namespace OmniSharp.Models.V2
{
    public static class SymbolKinds
    {
        // types
        public static readonly string Class = nameof(Class).ToLowerInvariant();
        public static readonly string Delegate = nameof(Delegate).ToLowerInvariant();
        public static readonly string Enum = nameof(Enum).ToLowerInvariant();
        public static readonly string Interface = nameof(Interface).ToLowerInvariant();
        public static readonly string Struct = nameof(Struct).ToLowerInvariant();

        // members
        public static readonly string Constant = nameof(Constant).ToLowerInvariant();
        public static readonly string Constructor = nameof(Constructor).ToLowerInvariant();
        public static readonly string Destructor = nameof(Destructor).ToLowerInvariant();
        public static readonly string EnumMember = nameof(EnumMember).ToLowerInvariant();
        public static readonly string Event = nameof(Event).ToLowerInvariant();
        public static readonly string Field = nameof(Field).ToLowerInvariant();
        public static readonly string Indexer = nameof(Indexer).ToLowerInvariant();
        public static readonly string Method = nameof(Method).ToLowerInvariant();
        public static readonly string Operator = nameof(Operator).ToLowerInvariant();
        public static readonly string Property = nameof(Property).ToLowerInvariant();

        // other
        public static readonly string Namespace = nameof(Namespace).ToLowerInvariant();
        public static readonly string Unknown = nameof(Unknown).ToLowerInvariant();
    }
}
