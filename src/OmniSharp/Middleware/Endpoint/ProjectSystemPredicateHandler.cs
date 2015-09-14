namespace OmniSharp.Middleware.Endpoint
{
    class ProjectSystemPredicateHandler : IPredicateHandler
    {
        public string GetLanguageForFilePath(string filePath)
        {
            return "Projects";
        }
    }
}
