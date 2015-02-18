using System;
using System.Threading.Tasks;
using Microsoft.AspNet.Builder;
using Microsoft.AspNet.Hosting.Server;
using Microsoft.Framework.ConfigurationModel;

namespace OmniSharp.Stdio
{
    public class StdioServerFactory : IServerFactory
    {
        public IServerInformation Initialize(IConfiguration configuration)
        {
            return new StdioServerInforation();
        }

        public IDisposable Start(IServerInformation serverInformation, Func<object, Task> application)
        {
            if (serverInformation.GetType() != typeof(StdioServerInforation))
            {
                throw new ArgumentException("wrong server", "serverInformation");
            }
            return new StdioServer(Console.In, Console.Out, application);
        }
    }
}
