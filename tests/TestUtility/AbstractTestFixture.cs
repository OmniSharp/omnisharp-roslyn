using System.Collections.Generic;
using Microsoft.Extensions.Logging;
using TestUtility.Logging;
using Xunit.Abstractions;

namespace TestUtility
{
    public abstract class AbstractTestFixture
    {
        protected readonly ITestOutputHelper TestOutput;
        protected readonly ILoggerFactory LoggerFactory;

        protected AbstractTestFixture(ITestOutputHelper output)
        {
            this.TestOutput = output;
            this.LoggerFactory = new LoggerFactory()
                .AddXunit(output);
        }

        protected OmniSharpTestHost CreateEmptyOmniSharpHost()
        {
            var host = OmniSharpTestHost.Create(path: null, testOutput: this.TestOutput);
            host.AddFilesToWorkspace();
            return host;
        }

        protected OmniSharpTestHost CreateOmniSharpHost(string path = null, IEnumerable<KeyValuePair<string, string>> configurationData = null, DotNetCliVersion dotNetCliVersion = DotNetCliVersion.Current) =>
            OmniSharpTestHost.Create(path, this.TestOutput, configurationData, dotNetCliVersion);

        protected OmniSharpTestHost CreateOmniSharpHost(params TestFile[] testFiles) => 
            CreateOmniSharpHost(testFiles, null);

        protected OmniSharpTestHost CreateOmniSharpHost(TestFile[] testFiles, IEnumerable<KeyValuePair<string, string>> configurationData)
        {
            var host = OmniSharpTestHost.Create(path: null, testOutput: this.TestOutput, configurationData: configurationData);

            if (testFiles.Length > 0)
            {
                host.AddFilesToWorkspace(testFiles);
            }

            return host;
        }
    }
}
