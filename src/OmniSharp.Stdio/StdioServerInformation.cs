using Microsoft.AspNet.Builder;
using Microsoft.AspNet.Hosting.Server;

namespace OmniSharp.Stdio
{
    public class StdioServerInformation : IServerInformation
    {
        public string Name { get { return nameof(StdioServer); } }
    }
}
