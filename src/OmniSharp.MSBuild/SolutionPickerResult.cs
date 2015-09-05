namespace OmniSharp.MSBuild
{
    public class SolutionPickerResult
    {
        public SolutionPickerResult(string solution, string message = null)
        {
            Solution = solution;
            Message = message;
        }

        public string Solution { get; private set; }
        public string Message { get; private set; }
    }
}