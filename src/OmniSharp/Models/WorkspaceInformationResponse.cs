using System;
using System.Linq;
using System.Collections.Generic;
using OmniSharp.AspNet5;
using OmniSharp.MSBuild;

namespace OmniSharp.Models
{
    public class WorkspaceInformationResponse
    {
        public AspNet5WorkspaceInformation AspNet5 { get; set; }
        public MsBuildWorkspaceInformation MSBuild { get; set; }
    }
}
