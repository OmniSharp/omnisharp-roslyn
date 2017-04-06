using System.Threading.Tasks;
using Microsoft.CodeAnalysis;
using Microsoft.Extensions.Logging;
using OmniSharp.Mef;
using OmniSharp.Models;
using OmniSharp.Services;

namespace OmniSharp.DotNetTest.Services
{
    [OmniSharpHandler(OmnisharpEndpoints.V2.GetDotNetTestStartInfo, LanguageNames.CSharp)]
    public abstract class BaseTestService<TRequest, TResponse> : RequestHandler<TRequest, TResponse>
        where TRequest: Request
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

        protected abstract TResponse HandleRequest(TRequest request);

        public Task<TResponse> Handle(TRequest request)
        {
            var response = HandleRequest(request);
            return Task.FromResult(response);
        }
    }
}
