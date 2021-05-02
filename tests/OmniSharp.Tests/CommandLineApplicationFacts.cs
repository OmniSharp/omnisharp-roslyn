using System.Linq;
using Xunit;

namespace OmniSharp.Tests
{
    public class CommandLineApplicationFacts
    {
        [InlineData("-s", @"\path\to\my-solution\foo", "a=b")]
        [InlineData("--source", @"\path\to\my-solution\foo", "a=b")]
        [InlineData("-s", @"\path\to\my=solution\foo", "a=b")]
        [InlineData("--source", @"\path\to\my=solution\foo", "a=b")]
        [InlineData("a=b", "-s", @"\path\to\my-solution\foo")]
        [InlineData("a=b", "--source", @"\path\to\my-solution\foo")]
        [InlineData("a=b", "-s", @"\path\to\my=solution\foo")]
        [InlineData("a=b", "--source", @"\path\to\my=solution\foo")]
        [InlineData("a=b", null, null)]
        [Theory]
        public void AllowsEqualsSignInSolutionPath(string arg1, string arg2, string arg3)
        {
            var app = new CommandLineApplication();
            app.OnExecute(() => 0);
            app.Execute(new[] { arg1, arg2, arg3 });

            Assert.Single(app.OtherArgs);
            Assert.Equal("a=b", app.OtherArgs.First());
        }
    }
}
