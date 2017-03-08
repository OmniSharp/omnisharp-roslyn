using System.IO;

namespace TestUtility
{
    public class TestAssets
    {
        public static TestAssets Instance { get; } = new TestAssets();

        public string RootFolder { get; }
        public string TestAssetsFolder { get; }
        public string TestProjectsFolder { get; }

        private TestAssets()
        {
            RootFolder = FindRootFolder();
            TestAssetsFolder = Path.Combine(RootFolder, "test-assets");
            TestProjectsFolder = Path.Combine(TestAssetsFolder, "test-projects");
        }

        private static string FindRootFolder()
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
