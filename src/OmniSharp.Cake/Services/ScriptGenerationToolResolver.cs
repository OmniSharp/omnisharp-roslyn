using System.IO;
using System.Linq;
using OmniSharp.Cake.Configuration;

namespace OmniSharp.Cake.Services
{
    internal static class ScriptGenerationToolResolver
    {
        public static string GetExecutablePath(string rootPath, ICakeConfiguration configuration)
        {
            var toolPath = GetToolPath(rootPath, configuration);

            if (!Directory.Exists(toolPath))
            {
                return string.Empty;
            }

            var bakeryPath = GetLatestBakeryPath(toolPath);

            if (bakeryPath == null)
            {
                return string.Empty;
            }

            return Path.Combine(toolPath, bakeryPath, "tools", "Cake.Bakery.exe");
        }

        private static string GetToolPath(string rootPath, ICakeConfiguration configuration)
        {
            var toolPath = configuration.GetValue(Constants.Paths.Tools);
            return Path.Combine(rootPath, !string.IsNullOrWhiteSpace(toolPath) ? toolPath : "tools");
        }

        private static string GetLatestBakeryPath(string toolPath)
        {
            var directories = Directory.GetDirectories(toolPath, "cake.bakery*", SearchOption.TopDirectoryOnly);

            // TODO: Sort by semantic version?
            return directories.OrderByDescending(x => x).FirstOrDefault();
        }
    }
}
