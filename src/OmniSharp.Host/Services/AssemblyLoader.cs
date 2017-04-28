using System;
using System.Reflection;
using Microsoft.Extensions.Logging;
using OmniSharp.Services;

namespace OmniSharp.Host.Loader
{
    internal class AssemblyLoader : IAssemblyLoader
    {
        private readonly ILogger _logger;

        public AssemblyLoader(ILoggerFactory loggerFactory)
        {
            this._logger = loggerFactory.CreateLogger<AssemblyLoader>();
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
    }
}
