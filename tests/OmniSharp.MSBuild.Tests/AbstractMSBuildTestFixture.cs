using System;
using System.Collections.Generic;
using System.Composition.Hosting.Core;
using Microsoft.CodeAnalysis;
using Microsoft.Extensions.Logging;
using OmniSharp.Host.Services;
using OmniSharp.MSBuild.Discovery;
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
            _analyzerAssemblyLoader = new AnalyzerAssemblyLoader();
            _msbuildLocator = MSBuildLocator.CreateStandAlone(this.LoggerFactory, _assemblyLoader);

            // Some tests require MSBuild to be discovered early
            // to ensure that the Microsoft.Build.* assemblies can be located
            _msbuildLocator.RegisterDefaultInstance(this.LoggerFactory.CreateLogger("MSBuildTests"));
        }

        public void Dispose()
        {
            (_msbuildLocator as IDisposable)?.Dispose();
        }

        protected OmniSharpTestHost CreateMSBuildTestHost(string path, IEnumerable<ExportDescriptorProvider> additionalExports = null,
            IEnumerable<KeyValuePair<string, string>> configurationData = null)
        {
            var environment = new OmniSharpEnvironment(path, logLevel: LogLevel.Trace);
            var serviceProvider = TestServiceProvider.Create(this.TestOutput, environment, this.LoggerFactory, _assemblyLoader, _analyzerAssemblyLoader, _msbuildLocator,
                configurationData);

            return OmniSharpTestHost.Create(serviceProvider, additionalExports);
        }
    }
}
