namespace OmniSharp.Middleware.Endpoint
{
    interface IPredicateHandler
    {
        string GetLanguageForFilePath(string filePath);
    }
}
