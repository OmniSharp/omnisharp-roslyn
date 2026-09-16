using System.Collections.Generic;
using System.Composition.Hosting.Core;
using System.IO;
using Microsoft.CodeAnalysis;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using OmniSharp.Roslyn.Utilities;
using OmniSharp.Services;
using TestUtility;
using Xunit.Abstractions;

namespace OmniSharp.MSBuild.Tests
{
    public abstract class AbstractMSBuildTestFixture : AbstractTestFixture
    {
        private readonly IAssemblyLoader _assemblyLoader;

        public AbstractMSBuildTestFixture(ITestOutputHelper output)
            : base(output)
        {
            _assemblyLoader = new AssemblyLoader(this.LoggerFactory);
        }

        protected OmniSharpTestHost CreateMSBuildTestHost(string path, IEnumerable<ExportDescriptorProvider> additionalExports = null,
            IConfiguration configurationData = null)
        {
            var environment = new OmniSharpEnvironment(path, logLevel: LogLevel.Trace);
            var serviceProvider = TestServiceProvider.Create(this.TestOutput, environment, this.LoggerFactory, _assemblyLoader, ShadowCopyAnalyzerAssemblyLoader.CreateShadowCopyLoader(),
                configurationData);

            return OmniSharpTestHost.Create(serviceProvider, additionalExports);
        }
    }
}
