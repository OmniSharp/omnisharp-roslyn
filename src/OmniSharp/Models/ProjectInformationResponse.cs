using OmniSharp.AspNet5;
using OmniSharp.MSBuild.ProjectFile;

namespace OmniSharp.Models
{
    public class ProjectInformationResponse
    {
        public MSBuildProject MsBuildProject { get; set; }
        public AspNet5Project AspNet5Project { get; set; }
    }
}