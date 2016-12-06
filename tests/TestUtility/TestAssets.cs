using System.IO;

namespace TestUtility
{
    public class TestAssets
    {
        public static TestAssets Instance { get; } = new TestAssets();

        public string SolutionFolder { get; }
        public string TestAssetsFolder { get; }
        public string TestProjectsFolder { get; }

        private TestAssets()
        {
            SolutionFolder = FindSolutionFolder();
            TestAssetsFolder = Path.Combine(SolutionFolder, "test-assets");
            TestProjectsFolder = Path.Combine(TestAssetsFolder, "test-projects");
        }

        private static string FindSolutionFolder()
        {
            var current = Directory.GetCurrentDirectory();
            while (!File.Exists(Path.Combine(current, "OmniSharp.sln")))
            {
                current = Path.GetDirectoryName(current);
                if (Path.GetPathRoot(current) == current)
                {
                    break;
                }
            }

            return current;
        }

        public string GetTestProjectFolder(string name)
        {
            return Path.Combine(TestProjectsFolder, name);
        }
    }
}
