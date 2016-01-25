using System;
using System.IO;
using Microsoft.AspNetCore.Hosting.Server;
using Microsoft.Extensions.Configuration;
using OmniSharp.Stdio.Services;

namespace OmniSharp.Stdio
{
    public class StdioServerFactory : IServerFactory
    {
        private readonly Func<IServer> _serverFactory;

        public StdioServerFactory(TextReader input, ISharedTextWriter output)
        {
            _serverFactory = () => new StdioServer(input, output);
        }

        public IServer CreateServer(IConfiguration configuration)
        {
            return _serverFactory();
        }
    }
}
