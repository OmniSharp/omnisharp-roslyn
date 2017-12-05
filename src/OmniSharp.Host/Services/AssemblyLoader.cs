using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.IO;
using System.Reflection;
using Microsoft.Extensions.Logging;
using OmniSharp.Utilities;

namespace OmniSharp.Services
{
    internal class AssemblyLoader : IAssemblyLoader
    {
        private static readonly ConcurrentDictionary<string, Assembly> AssemblyCache = new ConcurrentDictionary<string, Assembly>(
            PlatformHelper.IsWindows ? StringComparer.OrdinalIgnoreCase : StringComparer.Ordinal);
        private readonly ILogger _logger;

        public AssemblyLoader(ILoggerFactory loggerFactory)
        {
            _logger = loggerFactory.CreateLogger<AssemblyLoader>();
        }

        public Assembly Load(AssemblyName name)
        {
            Assembly result;
            try
            {
                result = Assembly.Load(name);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Failed to load assembly: {name}");
                throw;
            }

            _logger.LogTrace($"Assembly loaded: {name}");
            return result;
        }

        public IReadOnlyList<Assembly> LoadAllFrom(string folderPath)
        {
            if (string.IsNullOrWhiteSpace(folderPath)) return Array.Empty<Assembly>();

            try
            {
                var assemblies = new List<Assembly>();

                foreach (var filePath in Directory.EnumerateFiles(folderPath, "*.dll"))
                {
                    var assembly = LoadFrom(filePath);
                    if (assembly != null)
                    {
                        assemblies.Add(assembly);
                    }
                }

                return assemblies;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"An error occurred when attempting to access '{folderPath}'.");
                return Array.Empty<Assembly>();
            }
        }

        public Assembly LoadFrom(string assemblyPath)
        {
            if (string.IsNullOrWhiteSpace(assemblyPath)) return null;

            Assembly assembly = null;

            try
            {
                assembly = Assembly.LoadFrom(assemblyPath);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Failed to load assembly from path: {assemblyPath}");
            }

            _logger.LogTrace($"Assembly loaded from path: {assemblyPath}");
            return assembly;
        }

        public Assembly LoadFrom(string assemblyPath, bool lockAssembly)
        {
            if (lockAssembly) return LoadFrom(assemblyPath);
            if (string.IsNullOrWhiteSpace(assemblyPath)) return null;

            if (!AssemblyCache.TryGetValue(assemblyPath, out var assembly))
            {
                try
                {
                    var bytes = File.ReadAllBytes(assemblyPath);
                    assembly = Assembly.Load(bytes);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, $"Failed to load assembly from path: {assemblyPath}");
                }
                if (assembly != null)
                {
                    AssemblyCache.AddOrUpdate(assemblyPath, assembly, (k, v) => assembly);
                }
            }

            _logger.LogTrace($"Assembly loaded from path: {assemblyPath}");
            return assembly;
        }
    }
}
