using System;
using System.Collections.Immutable;
using System.IO;
using System.Reflection;
using System.Text;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using OmniSharp.MSBuild.Discovery.Providers;
using OmniSharp.Services;
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
        private readonly IAssemblyLoader _assemblyLoader;
        private readonly ImmutableArray<MSBuildInstanceProvider> _providers;

        public MSBuildInstance RegisteredInstance { get; private set; }

        private MSBuildLocator(ILoggerFactory loggerFactory, IAssemblyLoader assemblyLoader, ImmutableArray<MSBuildInstanceProvider> providers)
        {
            _logger = loggerFactory.CreateLogger<MSBuildLocator>();
            _assemblyLoader = assemblyLoader;
            _providers = providers;
        }

        protected override void DisposeCore(bool disposing)
        {
            if (RegisteredInstance != null)
            {
                AppDomain.CurrentDomain.AssemblyResolve -= Resolve;
                RegisteredInstance = null;
            }
        }

        public static MSBuildLocator CreateDefault(ILoggerFactory loggerFactory, IAssemblyLoader assemblyLoader, IConfiguration msbuildConfiguration)
            => new MSBuildLocator(loggerFactory, assemblyLoader,
                ImmutableArray.Create<MSBuildInstanceProvider>(
                    new DevConsoleInstanceProvider(loggerFactory),
                    new VisualStudioInstanceProvider(loggerFactory),
                    new MonoInstanceProvider(loggerFactory),
                    new StandAloneInstanceProvider(loggerFactory),
                    new UserOverrideInstanceProvider(loggerFactory, msbuildConfiguration)));

        public static MSBuildLocator CreateStandAlone(ILoggerFactory loggerFactory, IAssemblyLoader assemblyLoader)
            => new MSBuildLocator(loggerFactory, assemblyLoader,
                ImmutableArray.Create<MSBuildInstanceProvider>(
                    new StandAloneInstanceProvider(loggerFactory)));

        public void RegisterInstance(MSBuildInstance instance)
        {
            if (RegisteredInstance != null)
            {
                throw new InvalidOperationException("An MSBuild instance is already registered.");
            }

            RegisteredInstance = instance ?? throw new ArgumentNullException(nameof(instance));

            foreach (var assemblyName in s_msbuildAssemblies)
            {
                LoadAssemblyByNameOnly(assemblyName);
            }

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

            var builder = new StringBuilder();
            builder.Append($"Registered MSBuild instance: {instance}");

            foreach (var kvp in instance.PropertyOverrides)
            {
                builder.Append($"{Environment.NewLine}            {kvp.Key} = {kvp.Value}");
            }

            _logger.LogInformation(builder.ToString());
        }

        private Assembly Resolve(object sender, ResolveEventArgs e)
        {
            var assemblyName = new AssemblyName(e.Name);

            _logger.LogDebug($"Attempting to resolve '{assemblyName}'");

            return s_msbuildAssemblies.Contains(assemblyName.Name)
                ? LoadAssemblyByNameOnly(assemblyName.Name)
                : LoadAssemblyByFullName(assemblyName);
        }

        private Assembly LoadAssemblyByNameOnly(string assemblyName)
        {
            var assemblyPath = Path.Combine(RegisteredInstance.MSBuildPath, assemblyName + ".dll");
            var result = File.Exists(assemblyPath)
                ? _assemblyLoader.LoadFrom(assemblyPath)
                : null;

            if (result != null)
            {
                _logger.LogDebug($"SUCCESS: Resolved to '{assemblyPath}' (name-only).");
            }

            return result;
        }

        private Assembly LoadAssemblyByFullName(AssemblyName assemblyName)
        {
            var assemblyPath = Path.Combine(RegisteredInstance.MSBuildPath, assemblyName.Name + ".dll");
            if (!File.Exists(assemblyPath))
            {
                _logger.LogDebug($"FAILURE: Could not locate '{assemblyPath}'.");
                return null;
            }

            if (!TryGetAssemblyName(assemblyPath, out var resultAssemblyName))
            {
                _logger.LogDebug($"FAILURE: Could not retrieve {nameof(AssemblyName)} for '{assemblyPath}'.");
                return null;
            }

            if (assemblyName.Name != resultAssemblyName.Name ||
                assemblyName.Version != resultAssemblyName.Version ||
                !AreEqual(assemblyName.GetPublicKeyToken(), resultAssemblyName.GetPublicKeyToken()))
            {
                _logger.LogDebug($"FAILURE: Found '{assemblyPath}' but name, '{resultAssemblyName}', did not match.");
                return null;
            }

            // Note: don't bother testing culture. If the assembly has a different culture than what we're
            // looking for, go ahead and use it.

            var resultAssembly = _assemblyLoader.LoadFrom(assemblyPath);

            if (resultAssembly != null)
            {
                _logger.LogDebug($"SUCCESS: Resolved to '{assemblyPath}'");
            }

            return resultAssembly;
        }

        private static bool AreEqual(byte[] array1, byte[] array2)
        {
            if (array1 == null)
            {
                return array2 == null;
            }

            if (array1 == null)
            {
                return false;
            }

            if (array1.Length != array2.Length)
            {
                return false;
            }

            for (int i = 0; i < array1.Length; i++)
            {
                if (array1[i] != array2[i])
                {
                    return false;
                }
            }

            return true;
        }

        private static bool TryGetAssemblyName(string assemblyPath, out AssemblyName assemblyName)
        {
            try
            {
                assemblyName = AssemblyName.GetAssemblyName(assemblyPath);
                return assemblyName != null;
            }
            catch
            {
                assemblyName = null;
                return false;
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
