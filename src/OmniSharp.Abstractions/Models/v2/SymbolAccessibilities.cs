namespace OmniSharp.Models.V2
{
    public static class SymbolAccessibilities
    {
        public static readonly string Internal = nameof(Internal).ToLowerInvariant();
        public static readonly string Private = nameof(Private).ToLowerInvariant();
        public static readonly string PrivateProtected = $"{Private} {Protected}";
        public static readonly string Protected = nameof(Protected).ToLowerInvariant();
        public static readonly string ProtectedInternal = $"{Protected} {Internal}";
        public static readonly string Public = nameof(Public).ToLowerInvariant();
    }
}
