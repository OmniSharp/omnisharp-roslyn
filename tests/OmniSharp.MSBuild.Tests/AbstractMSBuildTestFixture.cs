using System.Collections.Generic;
using System.Composition.Hosting.Core;
using Microsoft.CodeAnalysis;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using OmniSharp.MSBuild.Discovery;
using OmniSharp.Roslyn.Utilities;
using OmniSharp.Services;
using TestUtility;
using Xunit.Abstractions;

namespace OmniSharp.MSBuild.Tests
{
    public abstract class AbstractMSBuildTestFixture : AbstractTestFixture
    {
        private readonly IAssemblyLoader _assemblyLoader;
        private readonly IAnalyzerAssemblyLoader _analyzerAssemblyLoader;

        public AbstractMSBuildTestFixture(ITestOutputHelper output)
            : base(output)
        {
            _assemblyLoader = new AssemblyLoader(this.LoggerFactory);
            _analyzerAssemblyLoader = ShadowCopyAnalyzerAssemblyLoader.Instance;
        }

        protected OmniSharpTestHost CreateMSBuildTestHost(
            string path,
            IEnumerable<ExportDescriptorProvider> additionalExports = null,
            IConfiguration configurationData = null)
        {
            var environment = new OmniSharpEnvironment(path, logLevel: LogLevel.Trace);
            using var msbuildLocator = MSBuildLocator.CreateDefault(this.LoggerFactory, _assemblyLoader, configurationData);
            var serviceProvider = TestServiceProvider.Create(this.TestOutput, environment, this.LoggerFactory, _assemblyLoader, _analyzerAssemblyLoader, msbuildLocator,
                configurationData);

            return OmniSharpTestHost.Create(serviceProvider, additionalExports);
        }
    }
}
