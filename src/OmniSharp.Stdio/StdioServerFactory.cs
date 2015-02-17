using System;
using System.Threading.Tasks;
using Microsoft.AspNet.Builder;
using Microsoft.AspNet.Hosting.Server;
using Microsoft.Framework.ConfigurationModel;

namespace OmniSharp.Stdio
{
    public class StdioServerFactory : IServerFactory
    {
        private class ServerInformation : IServerInformation
        {
            public string Name { get { return nameof(StdioServer); } }
        }

        public IServerInformation Initialize(IConfiguration configuration)
        {
            return new ServerInformation();
        }

        public IDisposable Start(IServerInformation serverInformation, Func<object, Task> application)
        {
            if (!(serverInformation.GetType() == typeof(ServerInformation)))
            {
                throw new ArgumentException("wrong server", "serverInformation");
            }
            return new StdioServer(Console.In, Console.Out, application);
        }
    }
}
