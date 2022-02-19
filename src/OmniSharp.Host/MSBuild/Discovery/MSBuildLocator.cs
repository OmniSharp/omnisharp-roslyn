using System;
using System.Collections.Immutable;
using System.IO;
using System.Reflection;
//using System.Runtime.Loader;
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
        private const string MSBuildPublicKeyToken = "b03f5f7f11d50a3a";

        private static readonly string[] s_msBuildAssemblies =
        {
            "Microsoft.Build",
            "Microsoft.Build.Framework",
            "Microsoft.Build.Tasks.Core",
            "Microsoft.Build.Utilities.Core",
        };

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
            return new MSBuildLocator(loggerFactory, assemblyLoader,
                ImmutableArray.Create<MSBuildInstanceProvider>(
                    new MicrosoftBuildLocatorInstanceProvider(loggerFactory),
#if !NETCOREAPP
                    new MonoInstanceProvider(loggerFactory),
#endif
                    new UserOverrideInstanceProvider(loggerFactory, msbuildConfiguration)));
        }

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

            if (MicrosoftBuildLocator.CanRegister)
            {
                MicrosoftBuildLocator.RegisterMSBuildPath(instance.MSBuildPath);
            }

            foreach (var msBuildAssemblyName in s_msBuildAssemblies)
            {
                var assembly = Assembly.Load($"{msBuildAssemblyName}, Version=15.1.0.0, Culture=neutral, PublicKeyToken={MSBuildPublicKeyToken}");
                if (assembly is null)
                {
                    throw new Exception($"Unable to load {msBuildAssemblyName}'");
                }
                _logger.LogInformation($"Loaded {msBuildAssemblyName} from '{assembly.Location}'");
            }
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
