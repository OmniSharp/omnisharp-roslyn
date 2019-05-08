using OmniSharp;
using OmniSharp.FileWatching;
using OmniSharp.Models;
using OmniSharp.Models.CodeCheck;
using OmniSharp.Models.FilesChanged;
using OmniSharp.Models.WorkspaceInformation;
using OmniSharp.MSBuild.Models;
using OmniSharp.Roslyn.CSharp.Services.Diagnostics;
using OmniSharp.Roslyn.CSharp.Services.Files;
using OmniSharp.Services;
using System.IO;
using System.Linq;
using System.Threading.Tasks;

namespace TestUtility
{
    public static class TestHostExtensions
    {
        public static CodeCheckService GetCodeCheckService(this OmniSharpTestHost host)
            => host.GetRequestHandler<CodeCheckService>(OmniSharpEndpoints.CodeCheck);

        public static WorkspaceInformationService GetWorkspaceInformationService(this OmniSharpTestHost host)
            => host.GetRequestHandler<WorkspaceInformationService>(OmniSharpEndpoints.WorkspaceInformation, "Projects");

        public static OnFilesChangedService GetFilesChangedService(this OmniSharpTestHost host)
            => host.GetRequestHandler<OnFilesChangedService>(OmniSharpEndpoints.FilesChanged);

        public static async Task<OmniSharpTestHost> RestoreProject(this OmniSharpTestHost host, ITestProject testProject)
        {
            await host.GetExport<IDotNetCliService>().RestoreAsync(testProject.Directory);

            var assetPath = Path.Combine(testProject.Directory, "obj");

            var filesChangeRequests = Directory.GetFiles(assetPath)
                .Select(file => new FilesChangedRequest()
                {
                    FileName = file,
                    ChangeType = FileChangeType.Create
                });

            await host.GetFilesChangedService().Handle(filesChangeRequests);

            return host;
        }

        public static async Task<MSBuildWorkspaceInfo> RequestMSBuildWorkspaceInfoAsync(this OmniSharpTestHost host)
        {
            var service = host.GetWorkspaceInformationService();

            var request = new WorkspaceInformationRequest
            {
                ExcludeSourceFiles = false
            };

            var response = await service.Handle(request);

            return (MSBuildWorkspaceInfo)response["MsBuild"];
        }

        public static async Task<QuickFixResponse> RequestCodeCheckAsync(this OmniSharpTestHost host, string filePath = null)
        {
            var service = host.GetCodeCheckService();

            var request = filePath == null ? new CodeCheckRequest() : new CodeCheckRequest { FileName = filePath };

            return await service.Handle(request);
        }
    }
}
