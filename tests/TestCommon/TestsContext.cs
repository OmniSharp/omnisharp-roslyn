using System.IO;

namespace TestCommon
{
    public class TestsContext
    {
        public static TestsContext Default { get; } = new TestsContext();

        private TestsContext()
        {
            var currentDirectory = Directory.GetCurrentDirectory();
            var solutionFile = "OmniSharp.sln";
            while (!File.Exists(Path.Combine(currentDirectory, solutionFile)))
            {
                currentDirectory = Path.GetDirectoryName(currentDirectory);
                if (Path.GetPathRoot(currentDirectory) == currentDirectory)
                {
                    break;
                }
            }

            SolutionRoot = currentDirectory;
            TestRoot = Path.Combine(SolutionRoot, "tests");
            TestSamples = Path.Combine(TestRoot, "TestSamples");
        }

        public string SolutionRoot { get; }

        public string TestRoot { get; }

        public string TestSamples { get; }

        public string GetTestSample(string name) => Path.Combine(TestSamples, name);
    }
}