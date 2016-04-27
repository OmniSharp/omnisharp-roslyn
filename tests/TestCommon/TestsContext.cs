using System;
using System.IO;

namespace TestCommon
{
    public class TestsContext
    {
        public static TestsContext Default { get; } = new TestsContext();

        private TestsContext()
        {
            var basedir = AppContext.BaseDirectory;
            var current = basedir;
            var solutionFile = "OmniSharp.sln";
            while (!File.Exists(Path.Combine(current, solutionFile)))
            {
                current = Path.GetDirectoryName(current);
                if (Path.GetPathRoot(current) == current)
                {
                    break;
                }
            }

            SolutionRoot = current;
            TestRoot = Path.Combine(SolutionRoot, "tests");
            TestSamples = Path.Combine(TestRoot, "TestSamples");
        }

        public string SolutionRoot { get; }

        public string TestRoot { get; }

        public string TestSamples { get; }

        public string GetTestSample(string name) => Path.Combine(TestSamples, name);
    }
}