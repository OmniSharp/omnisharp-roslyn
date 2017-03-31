using System.Composition;
using System.IO;
using System.Threading.Tasks;
using Microsoft.CodeAnalysis;
using Microsoft.Extensions.Logging;
using OmniSharp.DotNetTest.Models;
using OmniSharp.Mef;
using OmniSharp.Services;

namespace OmniSharp.DotNetTest.Services
{
    [OmniSharpHandler(OmnisharpEndpoints.V2.RunDotNetTest, LanguageNames.CSharp)]
    public class RunDotNetTestService : RequestHandler<RunDotNetTestRequest, RunDotNetTestResponse>
    {
        private readonly OmniSharpWorkspace _workspace;
        private readonly DotNetCliService _dotNetCli;
        private readonly ILoggerFactory _loggerFactory;

        [ImportingConstructor]
        public RunDotNetTestService(OmniSharpWorkspace workspace, DotNetCliService dotNetCli, ILoggerFactory loggerFactory)
        {
            _workspace = workspace;
            _dotNetCli = dotNetCli;
            _loggerFactory = loggerFactory;
        }

        public Task<RunDotNetTestResponse> Handle(RunDotNetTestRequest request)
        {
            var document = _workspace.GetDocument(request.FileName);

            using (var dtm = TestManager.Start(document.Project, _dotNetCli, _loggerFactory))
            {
                RunDotNetTestResponse response;

                response = dtm.IsConnected
                    ? dtm.RunTest(request.MethodName, request.TestFrameworkName)
                    : new RunDotNetTestResponse { Failure = "Failed to connect to 'dotnet test' process", Pass = false };

                return Task.FromResult(response);
            }
        }
    }
}
