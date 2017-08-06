using System;
using System.IO;
using System.Threading.Tasks;

namespace TestUtility
{
    public partial class TestAssets
    {
        public static TestAssets Instance { get; } = new TestAssets();

        public string RootFolder { get; }
        public string OmniSharpSolutionPath { get; }
        public string TestAssetsFolder { get; }
        public string TestProjectsFolder { get; }
        public string TestBinariesFolder { get; }

        private TestAssets()
        {
            RootFolder = FindRootFolder();
            OmniSharpSolutionPath = Path.Combine(RootFolder, "OmniSharp.sln");
            TestAssetsFolder = Path.Combine(RootFolder, "test-assets");
            TestProjectsFolder = Path.Combine(TestAssetsFolder, "test-projects");
            TestBinariesFolder = Path.Combine(TestAssetsFolder, "binaries");
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

        private static async Task CopyDirectoryAsync(DirectoryInfo sourceDirectory, DirectoryInfo destDirectory, bool recursive = true)
        {
            if (!sourceDirectory.Exists)
            {
                throw new InvalidOperationException($"Source directory does not exist: '{sourceDirectory.FullName}'.");
            }

            if (!destDirectory.Exists)
            {
                Directory.CreateDirectory(destDirectory.FullName);
            }

            foreach (var file in sourceDirectory.GetFiles())
            {
                var destFileName = Path.Combine(destDirectory.FullName, file.Name);
                using (var sourceStream = File.OpenRead(file.FullName))
                using (var destStream = File.Create(destFileName))
                    await sourceStream.CopyToAsync(destStream);
            }

            if (recursive)
            {
                foreach (var sourceSubDirectory in sourceDirectory.GetDirectories())
                {
                    var destSubDirectory = new DirectoryInfo(Path.Combine(destDirectory.FullName, sourceSubDirectory.Name));
                    await CopyDirectoryAsync(sourceSubDirectory, destSubDirectory, recursive);
                }
            }
        }

        public async Task<ITestProject> GetTestProjectAsync(string name, bool shadowCopy = true)
        {
            var sourceDirectory = Path.Combine(TestProjectsFolder, name);
            if (!shadowCopy)
            {
                return new TestProject(name, TestProjectsFolder, sourceDirectory, shadowCopied: false);
            }

            var baseDirectory = Path.Combine(Path.GetTempPath(), Path.GetRandomFileName());
            var targetDirectory = Path.Combine(baseDirectory, name);

            await CopyDirectoryAsync(new DirectoryInfo(sourceDirectory), new DirectoryInfo(targetDirectory));

            return new TestProject(name, baseDirectory, targetDirectory, shadowCopied: true);
        }
    }
}
