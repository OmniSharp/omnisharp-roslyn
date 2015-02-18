using Microsoft.AspNet.Builder;

namespace OmniSharp.Stdio
{
    public class StdioServerInforation : IServerInformation
    {
        public string Name { get { return nameof(StdioServer); } }
    }
}