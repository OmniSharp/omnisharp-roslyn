using OmniSharp.Dnx;
using OmniSharp.MSBuild.ProjectFile;

namespace OmniSharp.Models
{
    public class ProjectInformationResponse
    {
        public MSBuildProject MsBuildProject { get; set; }
        public DnxProject DnxProject { get; set; }
    }
}