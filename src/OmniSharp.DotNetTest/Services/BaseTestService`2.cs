using System.Threading.Tasks;
using Microsoft.CodeAnalysis;
using Microsoft.Extensions.Logging;
using OmniSharp.Mef;
using OmniSharp.Models;
using OmniSharp.Services;

namespace OmniSharp.DotNetTest.Services
{
    [OmniSharpHandler(OmnisharpEndpoints.V2.GetTestStartInfo, LanguageNames.CSharp)]
    internal abstract class BaseTestService<TRequest, TResponse> : BaseTestService, RequestHandler<TRequest, TResponse>
        where TRequest: Request
    {
        protected BaseTestService(OmniSharpWorkspace workspace, DotNetCliService dotNetCli, IEventEmitter eventEmitter, ILoggerFactory loggerFactory)
            : base(workspace, dotNetCli, eventEmitter, loggerFactory)
        {
        }

        protected abstract TResponse HandleRequest(TRequest request, TestManager testManager);

        public Task<TResponse> Handle(TRequest request)
        {
            using (var testManager = CreateTestManager(request.FileName))
            {
                var response = HandleRequest(request, testManager);
                return Task.FromResult(response);
            }
        }
    }
}
