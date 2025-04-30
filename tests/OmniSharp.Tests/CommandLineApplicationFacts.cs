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
            const string expectedLocale = "es-ES";

            var app = new CommandLineApplication();
            var runtimeLocale = string.Empty;
            app.OnExecute(() => { runtimeLocale = CultureInfo.CurrentUICulture.Name; return 0; });
            app.Execute(["--locale", expectedLocale]);

            Assert.Equal(expectedLocale, runtimeLocale);
        }

        [Fact]
        [UseCulture("de-DE", "de-DE")]
        public void PassingInvalidLocaleUsesSystemLocale()
        {
            const string expectedLocale = "de-DE";
            const string invalidLocale = "zz~ZZ";

            var app = new CommandLineApplication();
            var runtimeLocale = string.Empty;
            app.OnExecute(() => { runtimeLocale = CultureInfo.CurrentUICulture.Name; return 0; });
            app.Execute(["--locale", invalidLocale]);

            Assert.Equal(expectedLocale, runtimeLocale);
        }

        [Fact]
        [UseCulture("de-DE", "de-DE")]
        public void NotPassingLocaleUsesSystemLocale()
        {
            const string expectedLocale = "de-DE";

            var app = new CommandLineApplication();
            var runtimeLocale = string.Empty;
            app.OnExecute(() => { runtimeLocale = CultureInfo.CurrentUICulture.Name; return 0; });
            app.Execute([]);

            Assert.Equal(expectedLocale, runtimeLocale);
        }
    }
}
