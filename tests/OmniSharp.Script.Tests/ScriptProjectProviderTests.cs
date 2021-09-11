using System.IO;
using System.Linq;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.Scripting.Hosting;
using Microsoft.Extensions.Logging;
using OmniSharp.Services;
using Xunit;

namespace OmniSharp.Script.Tests
{
    public class ScriptProjectProviderTests
    {
        [Fact]
        public void DefaultLanguageVersionShouldBeLatest()
        {
            var scriptProjectProvider = new ScriptProjectProvider(new ScriptOptions(), new OmniSharpEnvironment(), new LoggerFactory(), true, false);
            var scriptProjectInfo = scriptProjectProvider.CreateProject("test.csx", Enumerable.Empty<MetadataReference>(), Path.GetTempPath(), typeof(CommandLineScriptGlobals));
            Assert.Equal(LanguageVersion.Latest, ((CSharpParseOptions)scriptProjectInfo.ParseOptions).SpecifiedLanguageVersion);
            Assert.Equal(LanguageVersion.CSharp10, ((CSharpParseOptions)scriptProjectInfo.ParseOptions).LanguageVersion);
        }
    }
}
