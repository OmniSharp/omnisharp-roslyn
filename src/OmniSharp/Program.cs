using System;
using System.Linq;
using System.IO;
using OmniSharp.Services;

namespace OmniSharp
{
    public class Program
    {
        private readonly IServiceProvider _serviceProvider;

        public Program(IServiceProvider serviceProvider)
        {
            _serviceProvider = serviceProvider;
        }

        public void Main(string[] args)
        {
            if (args.Length < 1)
            {
                Console.WriteLine("Usage: Omnisharp [-s /path/to/sln] [-p port]");
                return;
            }

            string applicationRoot = null;
            int? serverPort = null;
            var enumerator = args.GetEnumerator();

            while (enumerator.MoveNext())
            {
                var arg = (string)enumerator.Current;
                if (arg == "-s")
                {
                    enumerator.MoveNext();
                    applicationRoot = Path.GetFullPath((string)enumerator.Current);
                }
                else if (arg == "-p")
                {
                    enumerator.MoveNext();
                    serverPort = int.Parse((string)enumerator.Current);
                }
            }

            var program = new Microsoft.AspNet.Hosting.Program(new WrappedServiceProvider(_serviceProvider, applicationRoot));
            var mergedArgs = new[] { "--server", "Kestrel", "--server.urls", "http://localhost:" + serverPort };
            program.Main(mergedArgs);
        }

        // Wrap the service provider to provide the omnisharp services
        private class WrappedServiceProvider : IServiceProvider
        {
            private readonly IServiceProvider _sp;
            private readonly OmnisharpEnvironment _environment;

            public WrappedServiceProvider(IServiceProvider sp, string applicationRoot)
            {
                _sp = sp;
                _environment = new OmnisharpEnvironment(applicationRoot);
            }

            public object GetService(Type serviceType)
            {
                if (serviceType == typeof(IOmnisharpEnvironment))
                {
                    return _environment;
                }

                return _sp.GetService(serviceType);
            }
        }
    }
}