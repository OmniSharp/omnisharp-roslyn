using System;
using System.Reflection;
using Microsoft.Extensions.Logging;
using OmniSharp.Host.Loader;

namespace TestUtility.Annotate
{
    public class AnnotateAssemblyLoader: OmnisharpAssemblyLoader
    {
        private readonly ILogger _logger;
        
        public AnnotateAssemblyLoader(ILogger logger)
        {
            _logger = logger;
        }
        
        public override Assembly Load(AssemblyName name)
        {
            _logger.LogInformation($"Loading assembly {name}");
            
            try
            {
                return base.Load(name);
            }
            catch (Exception ex)
            {
                _logger.LogError($"Unexpected exception during assembly loading: {ex.Message}");
                throw;
            }
        }
    }
}