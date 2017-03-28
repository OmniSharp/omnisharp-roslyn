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
            var projectFolder = Path.GetDirectoryName(document.Project.FilePath);

            using (var dtm = DotNetTestManager.Start(projectFolder, _dotNetCli, _loggerFactory))
            {
                var response = dtm.ExecuteTestMethod(request.MethodName, request.TestFrameworkName);
                return Task.FromResult(response);
            }
        }
    }
}
