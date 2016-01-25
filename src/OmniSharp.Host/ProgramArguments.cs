using System.Collections.Generic;
using System.IO;
using Microsoft.Extensions.Logging;
using OmniSharp.Services;

namespace OmniSharp
{
    public class ProgramArguments
    {
        public static ProgramArguments Parse(string[] args)
        {
            var applicationRoot = Directory.GetCurrentDirectory();
            var serverPort = 2000;
            var logLevel = LogLevel.Information;
            var hostPID = -1;
            var transportType = TransportType.Http;
            var otherArgs = new List<string>();
            var plugins = new List<string>();

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
                else if (arg == "-v")
                {
                    logLevel = LogLevel.Debug;
                }
                else if (arg == "--hostPID")
                {
                    enumerator.MoveNext();
                    hostPID = int.Parse((string)enumerator.Current);
                }
                else if (arg == "--stdio")
                {
                    transportType = TransportType.Stdio;
                }
                else if (arg == "--plugin")
                {
                    enumerator.MoveNext();
                    plugins.Add((string)enumerator.Current);
                }
                else
                {
                    otherArgs.Add((string)enumerator.Current);
                }
            }

            return new ProgramArguments
            {
                Environment = new OmnisharpEnvironment(applicationRoot, serverPort, hostPID, logLevel, transportType, otherArgs.ToArray()),
                ServerPort = serverPort,
                Plugins = plugins,
                TransportType = transportType,
                HostProcesssID = hostPID
            };
        }

        private ProgramArguments() { }

        public int ServerPort { get; private set; }

        public OmnisharpEnvironment Environment { get; private set; }

        public IReadOnlyList<string> Plugins { get; private set; }

        public TransportType TransportType { get; private set; }

        public int HostProcesssID { get; private set; }
    }
}
