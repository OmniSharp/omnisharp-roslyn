using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using OmniSharp.Services;

namespace OmniSharp.DotNet.Tools
{
    internal class PackagesRestoreTool
    {
        private readonly ILogger _logger;
        private readonly IEventEmitter _emitter;

        public PackagesRestoreTool(ILoggerFactory logger, IEventEmitter emitter)
        {
            _logger = logger.CreateLogger<PackagesRestoreTool>();
            _emitter = emitter;
        }

        public void Restore(string projectPath)
        {
            _logger.LogWarning($"Packagee restore has not been implemented. Please manually restore project {projectPath}");
        }
    }
}
