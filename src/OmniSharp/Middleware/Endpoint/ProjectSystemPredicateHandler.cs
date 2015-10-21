namespace OmniSharp.Middleware.Endpoint
{
    class StaticLanguagePredicateHandler : IPredicateHandler
    {
        private readonly string _language;

        public StaticLanguagePredicateHandler(string language)
        {
            _language = language;
        }

        public string GetLanguageForFilePath(string filePath)
        {
            return _language;
        }
    }
}
