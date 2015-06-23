using System.Collections.Generic;
using System.Linq;
using OmniSharp.Dnx;

namespace OmniSharp.Models
{
    public class DnxWorkspaceInformation
    {
        private DnxContext _context;

        public DnxWorkspaceInformation(DnxContext context)
        {
            _context = context;

            Projects = context.Projects.Values.Select(p => new DnxProject(p));

            RuntimePath = context.RuntimePath;
            DesignTimeHostPort = context.DesignTimeHostPort;
        }

        public IEnumerable<DnxProject> Projects { get; }
        public string RuntimePath { get; }
        public int DesignTimeHostPort { get; }
    }

}
