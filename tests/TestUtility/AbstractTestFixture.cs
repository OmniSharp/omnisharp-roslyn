using System.Collections.Generic;
using System.Composition.Hosting.Core;
using Microsoft.Extensions.Logging;
using TestUtility.Logging;
using Xunit;
using Xunit.Abstractions;

namespace TestUtility
{
    public abstract class AbstractTestFixture : IClassFixture<SharedOmniSharpHostFixture>
    {
        protected readonly ITestOutputHelper TestOutput;
        protected readonly ILoggerFactory LoggerFactory;

        protected OmniSharpTestHost SharedOmniSharpTestHost { get; }


        protected AbstractTestFixture(ITestOutputHelper output)
        {
            TestOutput = output;
            LoggerFactory = new LoggerFactory()
                .AddXunit(output);
        }

        protected AbstractTestFixture(ITestOutputHelper output, SharedOmniSharpHostFixture sharedOmniSharpHostFixture)
        {
            TestOutput = output;
            LoggerFactory = new LoggerFactory()
                .AddXunit(output);

            if (sharedOmniSharpHostFixture.OmniSharpTestHost == null)
            {
                sharedOmniSharpHostFixture.OmniSharpTestHost = CreateSharedOmniSharpTestHost();
            }
            else
            {
                sharedOmniSharpHostFixture.OmniSharpTestHost.ClearWorkspace();
            }

            SharedOmniSharpTestHost = sharedOmniSharpHostFixture.OmniSharpTestHost;
        }

        protected virtual OmniSharpTestHost CreateSharedOmniSharpTestHost() => CreateOmniSharpHost();

        protected OmniSharpTestHost CreateEmptyOmniSharpHost()
        {
            var host = OmniSharpTestHost.Create(path: null, testOutput: this.TestOutput);
            host.AddFilesToWorkspace();
            return host;
        }

        protected OmniSharpTestHost CreateOmniSharpHost(
            string path = null,
            IEnumerable<KeyValuePair<string, string>> configurationData = null,
            DotNetCliVersion dotNetCliVersion = DotNetCliVersion.Current,
            IEnumerable<ExportDescriptorProvider> additionalExports = null)
            => OmniSharpTestHost.Create(path, this.TestOutput, configurationData, dotNetCliVersion, additionalExports);

        protected OmniSharpTestHost CreateOmniSharpHost(params TestFile[] testFiles) => 
            CreateOmniSharpHost(testFiles, null);

        protected OmniSharpTestHost CreateOmniSharpHost(TestFile[] testFiles, IEnumerable<KeyValuePair<string, string>> configurationData, string path = null)
        {
            var host = OmniSharpTestHost.Create(path: path, testOutput: this.TestOutput, configurationData: configurationData);

            if (testFiles.Length > 0)
            {
                host.AddFilesToWorkspace(path, testFiles);
            }

            return host;
        }
    }
}
