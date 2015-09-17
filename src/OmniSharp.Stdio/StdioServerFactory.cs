using System;
using System.IO;
using System.Threading.Tasks;
using Microsoft.AspNet.Builder;
using Microsoft.AspNet.FeatureModel;
using Microsoft.AspNet.Hosting.Server;
using Microsoft.Framework.Configuration;
using OmniSharp.Stdio.Services;

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
            return new StdioServerInformation();
        }

        public IDisposable Start(IServerInformation serverInformation, Func<IFeatureCollection, Task> application)
        {
            if (serverInformation.GetType() != typeof(StdioServerInformation))
            {
                throw new ArgumentException("wrong server", "serverInformation");
            }
            return new StdioServer(_input, _output, application);
        }
    }
}
