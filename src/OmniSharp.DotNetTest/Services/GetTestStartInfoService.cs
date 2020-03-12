using System.Composition;
using Microsoft.CodeAnalysis;
using Microsoft.Extensions.Logging;
using OmniSharp.DotNetTest.Models;
using OmniSharp.Eventing;
using OmniSharp.Mef;
using OmniSharp.Services;

namespace OmniSharp.DotNetTest.Services
{
    [OmniSharpHandler(OmniSharpEndpoints.V2.GetTestStartInfo, LanguageNames.CSharp)]
    internal class GetTestStartInfoService : BaseTestService<GetTestStartInfoRequest, GetTestStartInfoResponse>
    {
        [ImportingConstructor]
        public GetTestStartInfoService(OmniSharpWorkspace workspace, IDotNetCliService dotNetCli, IEventEmitter eventEmitter, ILoggerFactory loggerFactory)
            : base(workspace, dotNetCli, eventEmitter, loggerFactory)
        {
        }

        protected override GetTestStartInfoResponse HandleRequest(GetTestStartInfoRequest request, TestManager testManager)
        {
            return testManager.GetTestStartInfo(request.MethodName, request.RunSettings, request.TestFrameworkName, request.TargetFrameworkVersion);
        }
    }
}
