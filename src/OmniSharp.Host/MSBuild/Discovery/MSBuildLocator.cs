using System;
using System.Collections.Immutable;
using System.IO;
using System.Text;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using OmniSharp.MSBuild.Discovery.Providers;
using OmniSharp.Services;
using OmniSharp.Utilities;
using MicrosoftBuildLocator = Microsoft.Build.Locator.MSBuildLocator;

namespace OmniSharp.MSBuild.Discovery
{
    internal class MSBuildLocator : DisposableObject, IMSBuildLocator
    {
        private readonly ILogger _logger;
        private readonly ImmutableArray<MSBuildInstanceProvider> _providers;

        public MSBuildInstance RegisteredInstance { get; private set; }

        private MSBuildLocator(ILoggerFactory loggerFactory, IAssemblyLoader assemblyLoader, ImmutableArray<MSBuildInstanceProvider> providers)
        {
            _logger = loggerFactory.CreateLogger<MSBuildLocator>();
            _providers = providers;
        }

        protected override void DisposeCore(bool disposing)
        {
            if (RegisteredInstance != null)
            {
                RegisteredInstance = null;
            }
        }

        public static MSBuildLocator CreateDefault(ILoggerFactory loggerFactory, IAssemblyLoader assemblyLoader, IConfiguration msbuildConfiguration)
        {
            var useBundledOnly = msbuildConfiguration.GetValue<bool>("UseBundledOnly");
            if (useBundledOnly)
            {
                var logger = loggerFactory.CreateLogger<MSBuildLocator>();
                logger.LogInformation("Because 'UseBundledOnly' is enabled in the configuration, OmniSharp will only use the bundled MSBuild.");
                return CreateStandAlone(loggerFactory, assemblyLoader);
            }

            return new MSBuildLocator(loggerFactory, assemblyLoader,
                ImmutableArray.Create<MSBuildInstanceProvider>(
                    new MicrosoftBuildLocatorInstanceProvider(loggerFactory),
                    new MonoInstanceProvider(loggerFactory),
                    new StandAloneInstanceProvider(loggerFactory),
                    new UserOverrideInstanceProvider(loggerFactory, msbuildConfiguration)));
        }

        public static MSBuildLocator CreateStandAlone(ILoggerFactory loggerFactory, IAssemblyLoader assemblyLoader)
            => new MSBuildLocator(loggerFactory, assemblyLoader, ImmutableArray.Create<MSBuildInstanceProvider>(new StandAloneInstanceProvider(loggerFactory)));

        public void RegisterInstance(MSBuildInstance instance)
        {
            if (RegisteredInstance != null)
            {
                throw new InvalidOperationException("An MSBuild instance is already registered.");
            }

            RegisteredInstance = instance ?? throw new ArgumentNullException(nameof(instance));

            if (instance.SetMSBuildExePathVariable)
            {
                var msbuildExePath = Path.Combine(instance.MSBuildPath, "MSBuild.exe");
                var msbuildDllPath = Path.Combine(instance.MSBuildPath, "MSBuild.dll");

                string msbuildPath = null;
                if (File.Exists(msbuildExePath))
                {
                    msbuildPath = msbuildExePath;
                }
                else if (File.Exists(msbuildDllPath))
                {
                    msbuildPath = msbuildDllPath;
                }

                if (!string.IsNullOrEmpty(msbuildPath))
                {
                    Environment.SetEnvironmentVariable("MSBUILD_EXE_PATH", msbuildPath);
                    _logger.LogInformation($"MSBUILD_EXE_PATH environment variable set to '{msbuildPath}'");
                }
                else
                {
                    _logger.LogError("Could not find MSBuild executable path.");
                }
            }

            var builder = new StringBuilder();
            builder.Append($"Registered MSBuild instance: {instance}");

            foreach (var kvp in instance.PropertyOverrides)
            {
                builder.Append($"{Environment.NewLine}            {kvp.Key} = {kvp.Value}");
            }

            _logger.LogInformation(builder.ToString());

            if (!MicrosoftBuildLocator.CanRegister)
            {
                return;
            }

            MicrosoftBuildLocator.RegisterMSBuildPath(instance.MSBuildPath);
        }

        public ImmutableArray<MSBuildInstance> GetInstances()
        {
            var builder = ImmutableArray.CreateBuilder<MSBuildInstance>();

            foreach (var provider in _providers)
            {
                foreach (var instance in provider.GetInstances())
                {
                    if (instance != null)
                    {
                        builder.Add(instance);
                    }
                }
            }

            var result = builder.ToImmutable();
            LogInstances(result);
            return result;
        }

        private void LogInstances(ImmutableArray<MSBuildInstance> instances)
        {
            var builder = new StringBuilder();

            builder.Append($"Located {instances.Length} MSBuild instance(s)");
            for (int i = 0; i < instances.Length; i++)
            {
                builder.Append($"{Environment.NewLine}            {i + 1}: {instances[i]}");
            }

            _logger.LogInformation(builder.ToString());
        }
    }
}
