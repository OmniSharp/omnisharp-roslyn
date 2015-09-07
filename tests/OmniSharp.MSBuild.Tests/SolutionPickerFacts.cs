using OmniSharp.MSBuild;
using Xunit;

namespace OmniSharp.Tests
{
    public class SolutionPickerFacts
    {
        [Fact]
        public void SolutionPicker_picks_unity_solution()
        {
            var solutions = new[] { "unity.sln", "unity-csharp.sln" };
            var solution = SolutionPicker.ChooseSolution(null, solutions);

            Assert.Equal("unity-csharp.sln", solution.Solution);
        }

        [Fact]
        public void SolutionPicker_picks_only_solution()
        {
            var solutions = new[] { "unity.sln" };
            var solution = SolutionPicker.ChooseSolution(null, solutions);

            Assert.Equal("unity.sln", solution.Solution);
        }

        [Fact]
        public void SolutionPicker_logs_info_when_no_solutions_found()
        {
            var solutions = new string[0];
            var solution = SolutionPicker.ChooseSolution("/path", solutions);

            Assert.Null(solution.Solution);
            Assert.Equal("No solution files found in '/path'", solution.Message);
        }

        
        [Fact]
        public void SolutionPicker_logs_info_when_ambiguous_solutions_found()
        {
            var solutions = new[] { "unity.sln", "unity-csharp.sln", "another.sln" };
            var solution = SolutionPicker.ChooseSolution(null, solutions);

            Assert.Null(solution.Solution);
            Assert.Equal("Could not determine solution file", solution.Message);
        }
    }
}