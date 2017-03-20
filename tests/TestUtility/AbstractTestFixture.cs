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
            var host = OmniSharpTestHost.Create(path: null, testOutput: this.TestOutput, configurationData: null);
            host.AddFilesToWorkspace();
            return host;
        }

        protected OmniSharpTestHost CreateOmniSharpHost(string path = null, IEnumerable<KeyValuePair<string, string>> configurationData = null)
        {
            return OmniSharpTestHost.Create(path, this.TestOutput, configurationData);
        }

        protected OmniSharpTestHost CreateOmniSharpHost(params TestFile[] testFiles)
        {
            var host = OmniSharpTestHost.Create(path: null, testOutput: this.TestOutput, configurationData: null);

            if (testFiles.Length > 0)
            {
                host.AddFilesToWorkspace(testFiles);
            }

            return host;
        }
    }
}
