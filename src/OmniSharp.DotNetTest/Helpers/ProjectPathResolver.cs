using System.IO;

namespace OmniSharp.DotNetTest.Helpers
{
    internal class ProjectPathResolver
    {
        public static string GetProjectPathFromFile(string filepath)
        {
            // TODO: revisit this logic, too clumsy
            var projectFolder = Path.GetDirectoryName(filepath);
            while (!File.Exists(Path.Combine(projectFolder, "project.json")))
            {
                var parent = Path.GetDirectoryName(projectFolder);
                if (parent == projectFolder)
                {
                    break;
                }
                else
                {
                    projectFolder = parent;
                }
            }

            return projectFolder;
        }
    }
}
