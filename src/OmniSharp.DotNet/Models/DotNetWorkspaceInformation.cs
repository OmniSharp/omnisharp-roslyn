using System.Collections.Generic;

namespace OmniSharp.DotNet.Models
{
    internal class DotNetWorkspaceInformation
    {
        public IEnumerable<DotNetProjectInformation> Projects { get; } = new List<DotNetProjectInformation>();
    }
}
