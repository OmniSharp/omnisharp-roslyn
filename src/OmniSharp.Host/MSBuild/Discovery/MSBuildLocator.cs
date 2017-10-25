using System;
using System.Collections.Immutable;
using System.IO;
using System.Reflection;
using System.Text;
using Microsoft.Extensions.Logging;
using OmniSharp.MSBuild.Discovery.Providers;
using OmniSharp.Utilities;

namespace OmniSharp.MSBuild.Discovery
{
    internal class MSBuildLocator : DisposableObject, IMSBuildLocator
    {
        private static readonly ImmutableHashSet<string> s_msbuildAssemblies = ImmutableHashSet.Create(StringComparer.OrdinalIgnoreCase,
            "Microsoft.Build",
            "Microsoft.Build.Framework",
            "Microsoft.Build.Tasks.Core",
            "Microsoft.Build.Utilities.Core");

        private readonly ILogger _logger;
        private readonly ImmutableArray<MSBuildInstanceProvider> _providers;
        private MSBuildInstance _registeredInstance;

        public MSBuildInstance RegisteredInstance => _registeredInstance;

        private MSBuildLocator(ILoggerFactory loggerFactory, ImmutableArray<MSBuildInstanceProvider> providers)
        {
            _logger = loggerFactory.CreateLogger<MSBuildLocator>();
            _providers = providers;
        }

        protected override void DisposeCore(bool disposing)
        {
            if (_registeredInstance != null)
            {
                AppDomain.CurrentDomain.AssemblyResolve -= Resolve;
                _registeredInstance = null;
            }
        }

        public static MSBuildLocator CreateDefault(ILoggerFactory loggerFactory)
            => new MSBuildLocator(loggerFactory,
                ImmutableArray.Create<MSBuildInstanceProvider>(
                    new DevConsoleInstanceProvider(loggerFactory),
                    new VisualStudioInstanceProvider(loggerFactory),
                    new MonoInstanceProvider(loggerFactory),
                    new StandAloneInstanceProvider(loggerFactory, allowMonoPaths: true)));

        public static MSBuildLocator CreateStandAlone(ILoggerFactory loggerFactory, bool allowMonoPaths)
            => new MSBuildLocator(loggerFactory,
                ImmutableArray.Create<MSBuildInstanceProvider>(
                    new StandAloneInstanceProvider(loggerFactory, allowMonoPaths)));

        public void RegisterInstance(MSBuildInstance instance)
        {
            if (_registeredInstance != null)
            {
                throw new InvalidOperationException("An MSBuild instance is already registered.");
            }

            _registeredInstance = instance ?? throw new ArgumentNullException(nameof(instance));

            AppDomain.CurrentDomain.AssemblyResolve += Resolve;

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

            _logger.LogInformation($"Registered MSBuild instance: {instance}");
        }

        private Assembly Resolve(object sender, ResolveEventArgs e)
        {
            var assemblyName = new AssemblyName(e.Name);

            _logger.LogDebug($"Attempting to resolve '{assemblyName}'");

            if (s_msbuildAssemblies.Contains(assemblyName.Name))
            {
                var assemblyPath = Path.Combine(_registeredInstance.MSBuildPath, assemblyName.Name + ".dll");
                var result = File.Exists(assemblyPath)
                    ? Assembly.LoadFrom(assemblyPath)
                    : null;

                if (result != null)
                {
                    _logger.LogDebug($"Resolved '{assemblyName.Name}' to '{assemblyPath}'");
                    return result;
                }
            }

            return null;
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
