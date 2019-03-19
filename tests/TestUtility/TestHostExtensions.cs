using OmniSharp;
using OmniSharp.Models;
using OmniSharp.Models.CodeCheck;
using OmniSharp.Models.WorkspaceInformation;
using OmniSharp.MSBuild.Models;
using OmniSharp.Roslyn.CSharp.Services.Diagnostics;
using System.Threading.Tasks;

namespace TestUtility
{
    public static class TestHostExtensions
    {
        public static CodeCheckService GetCodeCheckService(this OmniSharpTestHost host)
            => host.GetRequestHandler<CodeCheckService>(OmniSharpEndpoints.CodeCheck);

        public static WorkspaceInformationService GetWorkspaceInformationService(this OmniSharpTestHost host)
            => host.GetRequestHandler<WorkspaceInformationService>(OmniSharpEndpoints.WorkspaceInformation, "Projects");

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
