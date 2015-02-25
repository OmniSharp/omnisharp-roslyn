using Microsoft.AspNet.Builder;

namespace OmniSharp.Stdio
{
    public class StdioServerInformation : IServerInformation
    {
        public string Name { get { return nameof(StdioServer); } }
    }
}
