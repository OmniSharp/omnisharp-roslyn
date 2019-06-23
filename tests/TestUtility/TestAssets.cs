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
        public string LegacyTestProjectsFolder { get; }
        public string TestProjectsFolder { get; }
        public string TestBinariesFolder { get; }
        public string TestScriptsFolder { get; }
        public string TestFilesFolder { get; }

        private TestAssets()
        {
            RootFolder = FindRootFolder();
            OmniSharpSolutionPath = Path.Combine(RootFolder, "OmniSharp.sln");
            TestAssetsFolder = Path.Combine(RootFolder, "test-assets");
            LegacyTestProjectsFolder = Path.Combine(TestAssetsFolder, "legacy-test-projects");
            TestProjectsFolder = Path.Combine(TestAssetsFolder, "test-projects");
            TestScriptsFolder = Path.Combine(TestAssetsFolder, "test-scripts");
            TestBinariesFolder = Path.Combine(TestAssetsFolder, "binaries");
            TestFilesFolder = Path.Combine(TestAssetsFolder, "test-files");
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
                await CopyFileAsync(file, destDirectory);
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

        private static async Task CopyFileAsync(FileInfo file, DirectoryInfo destDirectory)
        {
            var destFileName = Path.Combine(destDirectory.FullName, file.Name);
            using (var sourceStream = File.OpenRead(file.FullName))
            using (var destStream = File.Create(destFileName))
                await sourceStream.CopyToAsync(destStream);
        }

        public ITestProject GetTestScript(string folderName)
        {
            var sourceDirectory = Path.Combine(TestScriptsFolder, folderName);
            return new TestProject(folderName, TestScriptsFolder, sourceDirectory, shadowCopied: false);
        }

        public async Task<ITestProject> GetTestProjectAsync(string name, bool shadowCopy = true, bool legacyProject = false)
        {
            var testProjectsFolder = legacyProject
                ? LegacyTestProjectsFolder
                : TestProjectsFolder;

            var sourceDirectory = Path.Combine(testProjectsFolder, name);
            if (!shadowCopy)
            {
                return new TestProject(name, testProjectsFolder, sourceDirectory, shadowCopied: false);
            }

            var baseDirectory = Path.Combine(Path.GetTempPath(), Path.GetRandomFileName());
            var targetDirectory = Path.Combine(baseDirectory, name);

            await CopyDirectoryAsync(new DirectoryInfo(sourceDirectory), new DirectoryInfo(targetDirectory));

            var globalJsonFileInfo = new FileInfo(Path.Combine(testProjectsFolder, "global.json"));
            await CopyFileAsync(globalJsonFileInfo, new DirectoryInfo(baseDirectory));

            return new TestProject(name, baseDirectory, targetDirectory, shadowCopied: true);
        }
    }
}
