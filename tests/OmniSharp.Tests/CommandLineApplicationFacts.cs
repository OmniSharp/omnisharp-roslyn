using System.Globalization;
using System.Linq;
using TestUtility;
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
            app.Execute(new[] { arg1, arg2, arg3 }.OfType<string>().ToArray());

            Assert.Single(app.OtherArgs);
            Assert.Equal("a=b", app.OtherArgs.First());
        }

        [Fact]
        [UseCulture("de-DE", "de-DE")]
        public void PassingLocaleSetsRuntimeLocale()
        {
            const string locale = "es-ES";
            var runtimeLocale = string.Empty;

            var app = new CommandLineApplication();
            app.OnExecute(() => { runtimeLocale = CultureInfo.CurrentUICulture.Name; return 0; });
            app.Execute(["--locale", locale]);

            Assert.Equal(locale, runtimeLocale);
        }

        [Fact]
        [UseCulture("de-DE", "de-DE")]
        public void NotPassingLocaleUsesSystemLocale()
        {
            const string locale = "de-DE";
            var runtimeLocale = string.Empty;

            var app = new CommandLineApplication();
            app.OnExecute(() => { runtimeLocale = CultureInfo.CurrentUICulture.Name; return 0; });
            app.Execute([]);

            Assert.Equal(locale, runtimeLocale);
        }
    }
}
