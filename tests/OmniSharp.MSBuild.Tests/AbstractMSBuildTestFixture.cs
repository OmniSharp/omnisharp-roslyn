using System;
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
    public abstract class AbstractMSBuildTestFixture : AbstractTestFixture, IDisposable
    {
        private readonly IAssemblyLoader _assemblyLoader;
        private readonly IMSBuildLocator _msbuildLocator;
        private readonly IAnalyzerAssemblyLoader _analyzerAssemblyLoader;

        public AbstractMSBuildTestFixture(ITestOutputHelper output)
            : base(output)
        {
            _assemblyLoader = new AssemblyLoader(this.LoggerFactory);
            _analyzerAssemblyLoader = ShadowCopyAnalyzerAssemblyLoader.Instance;

            // Since we can only load MSBuild once into our process we need to include
            // prerelease version so that our .NET 7 tests will pass.
            var configuration = new Dictionary<string, string>
            {
                ["sdk:IncludePrereleases"] = bool.TrueString
            }.ToConfiguration();

            _msbuildLocator = MSBuildLocator.CreateDefault(this.LoggerFactory, _assemblyLoader, configuration);

            // Some tests require MSBuild to be discovered early
            // to ensure that the Microsoft.Build.* assemblies can be located
            _msbuildLocator.RegisterDefaultInstance(this.LoggerFactory.CreateLogger("MSBuildTests"), dotNetInfo: null);
        }

        public void Dispose()
        {
            (_msbuildLocator as IDisposable)?.Dispose();
        }

        protected OmniSharpTestHost CreateMSBuildTestHost(string path, IEnumerable<ExportDescriptorProvider> additionalExports = null,
            IConfiguration configurationData = null)
        {
            var environment = new OmniSharpEnvironment(path, logLevel: LogLevel.Trace);
            var serviceProvider = TestServiceProvider.Create(this.TestOutput, environment, this.LoggerFactory, _assemblyLoader, _analyzerAssemblyLoader, _msbuildLocator,
                configurationData);

            return OmniSharpTestHost.Create(serviceProvider, additionalExports);
        }
    }
}
