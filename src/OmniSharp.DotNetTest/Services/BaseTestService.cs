using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using OmniSharp.Eventing;
using OmniSharp.Services;

namespace OmniSharp.DotNetTest.Services
{
    internal abstract class BaseTestService
    {
        protected readonly OmniSharpWorkspace Workspace;
        protected readonly IDotNetCliService DotNetCli;
        protected readonly IEventEmitter EventEmitter;
        protected readonly ILoggerFactory LoggerFactory;

        protected BaseTestService(OmniSharpWorkspace workspace, IDotNetCliService dotNetCli, IEventEmitter eventEmitter, ILoggerFactory loggerFactory)
        {
            Workspace = workspace;
            DotNetCli = dotNetCli;
            EventEmitter = eventEmitter;
            LoggerFactory = loggerFactory;
        }

        protected async Task<TestManager> CreateTestManagerAsync(string fileName, bool noBuild, CancellationToken cancellationToken = default)
        {
            var document = Workspace.GetDocument(fileName);

            return await TestManager.Start(document.Project, DotNetCli, EventEmitter, LoggerFactory, noBuild, cancellationToken);
        }
    }
}
