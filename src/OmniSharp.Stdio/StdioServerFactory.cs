using System;
using System.Threading.Tasks;
using Microsoft.AspNet.Builder;
using Microsoft.AspNet.Hosting.Server;
using Microsoft.Framework.ConfigurationModel;
using OmniSharp.Stdio.Services;
using System.IO;

namespace OmniSharp.Stdio
{
    public class StdioServerFactory : IServerFactory
    {
        private readonly TextReader _input;
        private readonly ISharedTextWriter _output;

        public StdioServerFactory(TextReader input, ISharedTextWriter output)
        {
            _input = input;
            _output = output;
        }

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
            return new StdioServer(_input, _output, application);
        }
    }
}
