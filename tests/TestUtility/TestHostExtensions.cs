using OmniSharp.Models;
using OmniSharp.Models.CodeCheck;
using OmniSharp.Models.WorkspaceInformation;
using OmniSharp.MSBuild.Models;
using System.Threading.Tasks;

namespace TestUtility
{
    public static class TestHostExtensions
    {
        public static async Task<MSBuildWorkspaceInfo> GetMSBuildWorkspaceInfoAsync(this OmniSharpTestHost host)
        {
            var service = host.GetWorkspaceInformationService();

            var request = new WorkspaceInformationRequest
            {
                ExcludeSourceFiles = false
            };

            var response = await service.Handle(request);

            return (MSBuildWorkspaceInfo)response["MsBuild"];
        }

        public static async Task<QuickFixResponse> CodeCheckRequestAsync(this OmniSharpTestHost host, string filePath)
        {
            var service = host.GetCodeCheckServiceService();

            var request = new CodeCheckRequest { FileName = filePath };

            return await service.Handle(request);
        }
    }
}
