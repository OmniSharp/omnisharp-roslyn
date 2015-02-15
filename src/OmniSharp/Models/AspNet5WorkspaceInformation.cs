using System.Collections.Generic;
using System.Linq;
using OmniSharp.AspNet5;

namespace OmniSharp.Models
{
    public class AspNet5WorkspaceInformation
    {
        private AspNet5Context _context;

        public AspNet5WorkspaceInformation(AspNet5Context context)
        {
            _context = context;

            Projects = context.Projects.Values.Select(p => new AspNet5Project(p));

            RuntimePath = context.RuntimePath;
            DesignTimeHostPort = context.DesignTimeHostPort;
        }

        public IEnumerable<AspNet5Project> Projects { get; }
        public string RuntimePath { get; }
        public int DesignTimeHostPort { get; }
    }

}