using Microsoft.Extensions.Logging;
using OmniSharp.Services;

namespace OmniSharp.DotNetTest.Services
{
    public abstract class BaseTestService
    {
        private readonly OmniSharpWorkspace _workspace;
        private readonly DotNetCliService _dotNetCli;
        private readonly IEventEmitter _eventEmitter;
        private readonly ILoggerFactory _loggerFactory;

        protected BaseTestService(OmniSharpWorkspace workspace, DotNetCliService dotNetCli, IEventEmitter eventEmitter, ILoggerFactory loggerFactory)
        {
            _workspace = workspace;
            _dotNetCli = dotNetCli;
            _eventEmitter = eventEmitter;
            _loggerFactory = loggerFactory;
        }

        protected TestManager CreateTestManager(string fileName)
        {
            var document = _workspace.GetDocument(fileName);

            return TestManager.Start(document.Project, _dotNetCli, _eventEmitter, _loggerFactory);
        }
    }
}
