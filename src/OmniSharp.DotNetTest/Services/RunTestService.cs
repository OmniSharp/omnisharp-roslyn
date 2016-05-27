using System.Composition;
using System.Threading.Tasks;
using Microsoft.CodeAnalysis;
using Microsoft.Extensions.Logging;
using OmniSharp.DotNetTest.Helpers;
using OmniSharp.DotNetTest.Helpers.DotNetTestManager;
using OmniSharp.DotNetTest.Models;
using OmniSharp.Mef;

namespace OmniSharp.DotNetTest.Services
{
    [OmniSharpHandler(OmnisharpEndpoints.RunDotNetTest, LanguageNames.CSharp)]
    public class RunTestServices : RequestHandler<RunDotNetTestRequest, RunDotNetTestResponse>
    {
        private readonly ILoggerFactory _loggerFactory;
        private readonly ILogger _logger;

        [ImportingConstructor]
        public RunTestServices(ILoggerFactory loggerFactory)
        {
            _loggerFactory = loggerFactory;
            _logger = loggerFactory.CreateLogger<RunTestServices>();
        }

        public Task<RunDotNetTestResponse> Handle(RunDotNetTestRequest request)
        {
            return Task.FromResult(GetResponse(request.FileName, request.MethodName));
        }

        private RunDotNetTestResponse GetResponse(string filepath, string methodName)
        {
            var projectFolder = ProjectPathResolver.GetProjectPathFromFile(filepath);
            using (var dtm = DotNetTestManager.Start(projectFolder, _loggerFactory))
            {
                return dtm.ExecuteTestMethod(methodName);
            }
        }
    }
}