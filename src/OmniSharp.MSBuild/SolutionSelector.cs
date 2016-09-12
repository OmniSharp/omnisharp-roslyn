using System.Linq;

namespace OmniSharp.MSBuild
{
    public static class SolutionSelector
    {
        public static Result Pick(string[] solutionFilePaths, string path)
        {
            switch (solutionFilePaths.Length)
            {
                case 0:
                    return new Result(null, string.Format("No solution files found in '{0}'", path));
                case 1:
                    return new Result(solutionFilePaths[0]);
                case 2:
                    var unitySolution = solutionFilePaths.FirstOrDefault(s => s.EndsWith("-csharp.sln"));
                    if (unitySolution != null)
                    {
                        return new Result(unitySolution);
                    }

                    return new Result(null, "Could not determine solution file");
                default:
                    return new Result(null, "Could not determine solution file");
            }
        }

        public struct Result
        {
            public string Solution { get; }
            public string Message { get; }

            public Result(string solution, string message = null)
            {
                Solution = solution;
                Message = message;
            }
        }
    }
}
