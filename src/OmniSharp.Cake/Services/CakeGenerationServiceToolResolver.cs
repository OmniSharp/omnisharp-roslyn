using System.IO;
using OmniSharp.Cake.Configuration;

namespace OmniSharp.Cake.Services
{
    internal static class CakeGenerationServiceToolResolver
    {
        public static string GetServerExecutablePath(string rootPath, ICakeConfiguration configuration)
        {
            var toolPath = GetToolPath(rootPath, configuration);

            return Path.Combine(toolPath, "Cake.Bakery", "tools", "Cake.Bakery.exe");
        }

        private static string GetToolPath(string rootPath, ICakeConfiguration configuration)
        {
            var toolPath = configuration.GetValue(Constants.Paths.Tools);
            return Path.Combine(rootPath, !string.IsNullOrWhiteSpace(toolPath) ? toolPath : "tools");
        }
    }
}
