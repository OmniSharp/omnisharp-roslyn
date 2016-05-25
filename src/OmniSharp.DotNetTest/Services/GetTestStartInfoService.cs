using System.Composition;
using System.Threading.Tasks;
using Microsoft.CodeAnalysis;
using Microsoft.Extensions.Logging;
using OmniSharp.DotNetTest.Helpers;
using OmniSharp.DotNetTest.Models;
using OmniSharp.Mef;

namespace OmniSharp.DotNetTest.Services
{
    [OmniSharpHandler(OmnisharpEndpoints.GetTestStartInfo, LanguageNames.CSharp)]
    public class GetTestStartInfoService : RequestHandler<GetTestStartInfoRequest, GetTestStartInfoResponse>
    {
        private readonly ILoggerFactory _loggerFactory;

        [ImportingConstructor]
        public GetTestStartInfoService(ILoggerFactory loggerFactory)
        {
            _loggerFactory = loggerFactory;
        }

        public Task<GetTestStartInfoResponse> Handle(GetTestStartInfoRequest request)
        {
            var helper = new XunitTestDebugInfoHelper(request.MethodName, request.FileName, _loggerFactory);

            return Task.FromResult(helper.GetResponse());
        }
    }
}