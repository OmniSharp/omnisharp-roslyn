using OmniSharp.ConfigurationManager;

namespace OmniSharp.Roslyn.CSharp
{
    public static class StringExtensions
    {
        public static string ApplyPathReplacementsForClient(this string path)
        {
            if (ConfigurationLoader.Config.PathReplacements != null)
            {
                foreach (var pathReplacement in ConfigurationLoader.Config.PathReplacements)
                {
                    path = path.Replace(pathReplacement.To, pathReplacement.From);
                }
            }

            return path;
        }
    }
}
