using Microsoft.Extensions.Logging;
using OmniSharp.Eventing;
using OmniSharp.Services;

namespace OmniSharp.DotNetTest.Services
{
    internal abstract class BaseTestService
    {
        protected readonly OmniSharpWorkspace Workspace;
        protected readonly DotNetCliService DotNetCli;
        protected readonly IEventEmitter EventEmitter;
        protected readonly ILoggerFactory LoggerFactory;

        protected BaseTestService(OmniSharpWorkspace workspace, DotNetCliService dotNetCli, IEventEmitter eventEmitter, ILoggerFactory loggerFactory)
        {
            Workspace = workspace;
            DotNetCli = dotNetCli;
            EventEmitter = eventEmitter;
            LoggerFactory = loggerFactory;
        }

        protected TestManager CreateTestManager(string fileName)
        {
            var document = Workspace.GetDocument(fileName);

            return TestManager.Start(document.Project, DotNetCli, EventEmitter, LoggerFactory);
        }
    }
}
