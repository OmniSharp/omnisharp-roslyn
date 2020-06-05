using System.Threading.Tasks;
using Microsoft.CodeAnalysis;
using Microsoft.Extensions.Logging;
using OmniSharp.DotNetTest.Models;
using OmniSharp.Eventing;
using OmniSharp.Mef;
using OmniSharp.Models;
using OmniSharp.Services;

namespace OmniSharp.DotNetTest.Services
{
    [OmniSharpHandler(OmniSharpEndpoints.V2.GetTestStartInfo, LanguageNames.CSharp)]
    internal abstract class BaseTestService<TRequest, TResponse> : BaseTestService, IRequestHandler<TRequest, TResponse>
        where TRequest: BaseTestRequest
    {
        protected BaseTestService(OmniSharpWorkspace workspace, IDotNetCliService dotNetCli, IEventEmitter eventEmitter, ILoggerFactory loggerFactory)
            : base(workspace, dotNetCli, eventEmitter, loggerFactory)
        {
        }

        protected abstract Task<TResponse> HandleRequest(TRequest request, TestManager testManager);

        public async Task<TResponse> Handle(TRequest request)
        {
            using (var testManager = CreateTestManager(request.FileName, request.NoBuild))
            {
                return await HandleRequest(request, testManager);
            }
        }
    }
}
