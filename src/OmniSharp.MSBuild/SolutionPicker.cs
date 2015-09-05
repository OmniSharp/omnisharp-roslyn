using System.Linq;

namespace OmniSharp.MSBuild
{
    public static class SolutionPicker
    {
        public static SolutionPickerResult ChooseSolution(string path,
                                                          string[] solutions)
        {
            switch (solutions.Length)
            {
                case 0:
                    return new SolutionPickerResult(null, string.Format("No solution files found in '{0}'", path));
                case 1:
                    return new SolutionPickerResult(solutions[0]);
                case 2:
                    var unitySolution = solutions.FirstOrDefault(s => s.EndsWith("-csharp.sln"));
                    if (unitySolution != null)
                    {
                        return new SolutionPickerResult(unitySolution);
                    }
                    return new SolutionPickerResult(null, "Could not determine solution file");
                default:
                    return new SolutionPickerResult(null, "Could not determine solution file");
            }
        }
    }
}
