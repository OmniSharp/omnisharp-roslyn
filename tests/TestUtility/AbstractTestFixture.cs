using System;
using System.Collections.Generic;
using Microsoft.Extensions.Logging;
using TestUtility.Logging;
using Xunit.Abstractions;

namespace TestUtility
{
    public abstract class AbstractTestFixture : IDisposable
    {
        protected readonly ITestOutputHelper TestOutput;
        protected readonly ILoggerFactory LoggerFactory;
        protected static OmniSharpTestHost OmniSharpTestHost;

        protected AbstractTestFixture(ITestOutputHelper output)
        {
            TestOutput = output;
            LoggerFactory = new LoggerFactory()
                .AddXunit(output);

            if (UseSharedOmniSharpHost)
            {
                if (OmniSharpTestHost == null)
                {
                    OmniSharpTestHost = CreateOmniSharpHost();
                }
                else
                {
                    OmniSharpTestHost.ClearWorkspace();
                }
            }
        }

        protected virtual bool UseSharedOmniSharpHost => true;

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

        public virtual void Dispose()
        {
            OmniSharpTestHost?.Dispose();
        }
    }
}
