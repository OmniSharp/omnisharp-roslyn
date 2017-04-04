using System.Composition;
using Microsoft.CodeAnalysis;
using Microsoft.Extensions.Logging;
using OmniSharp.DotNetTest.Models;
using OmniSharp.Mef;
using OmniSharp.Services;

namespace OmniSharp.DotNetTest.Services
{
    [OmniSharpHandler(OmnisharpEndpoints.V2.GetDotNetTestStartInfo, LanguageNames.CSharp)]
    public class GetDotNetTestStartInfoService : BaseTestService<GetDotNetTestStartInfoRequest, GetDotNetTestStartInfoResponse>
    {
        [ImportingConstructor]
        public GetDotNetTestStartInfoService(OmniSharpWorkspace workspace, DotNetCliService dotNetCli, ILoggerFactory loggerFactory)
            : base(workspace, dotNetCli, loggerFactory)
        {
        }

        protected override GetDotNetTestStartInfoResponse HandleRequest(GetDotNetTestStartInfoRequest request, TestManager testManager)
        {
            return testManager.GetTestStartInfo(request.MethodName, request.TestFrameworkName);
        }
    }
}
