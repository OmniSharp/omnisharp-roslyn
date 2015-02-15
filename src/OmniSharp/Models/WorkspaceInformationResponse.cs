using System;
using System.Collections.Generic;
using System.Linq;
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
