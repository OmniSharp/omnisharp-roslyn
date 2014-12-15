using System;
using System.Diagnostics;
using System.IO;
using System.Net;
using System.Net.Sockets;
using System.Threading;
using Microsoft.Framework.Logging;

namespace OmniSharp.AspNet5
{
    public class DesignTimeHostManager
    {
        private readonly ILogger _logger;

        public DesignTimeHostManager(ILoggerFactory loggerFactory)
        {
            _logger = loggerFactory.Create<DesignTimeHostManager>();

            DelayBeforeRestart = TimeSpan.FromSeconds(1000);
        }

        public TimeSpan DelayBeforeRestart { get; set; }

        public void Start(string runtimePath, string hostId, Action<int> onConnected)
        {
            int port = GetFreePort();

            var psi = new ProcessStartInfo
            {
                FileName = Path.Combine(runtimePath, "bin", "klr"),
                CreateNoWindow = true,
                UseShellExecute = false,
                RedirectStandardError = true,
                Arguments = string.Format(@"{0} {1} {2} {3}",
                                          Path.Combine(runtimePath, "bin", "lib", "Microsoft.Framework.DesignTimeHost", "Microsoft.Framework.DesignTimeHost.dll"),
                                          port,
                                          Process.GetCurrentProcess().Id,
                                          hostId),
            };

#if ASPNET50
            psi.EnvironmentVariables["KRE_APPBASE"] = Directory.GetCurrentDirectory();
#else
            psi.Environment["KRE_APPBASE"] = Directory.GetCurrentDirectory();
#endif

            _logger.WriteVerbose(psi.FileName + " " + psi.Arguments);

            var kreProcess = Process.Start(psi);

            // Wait a little bit for it to conncet before firing the callback
            Thread.Sleep(1000);

            if (kreProcess.HasExited)
            {
                // REVIEW: Should we quit here or retry?
                _logger.WriteError(string.Format("Failed to launch DesignTimeHost. Process exited with code {0}.", kreProcess.ExitCode));
                return;
            }

            _logger.WriteInformation(string.Format("Running DesignTimeHost on port {0}, with PID {1}", port, kreProcess.Id));

            kreProcess.EnableRaisingEvents = true;
            kreProcess.Exited += (sender, e) =>
            {
                _logger.WriteWarning("Process ended. Restarting");

                Thread.Sleep(DelayBeforeRestart);

                Start(runtimePath, hostId, onConnected);
            };

            onConnected(port);
        }

        private static int GetFreePort()
        {
            var l = new TcpListener(IPAddress.Loopback, 0);
            l.Start();
            int port = ((IPEndPoint)l.LocalEndpoint).Port;
            l.Stop();
            return port;
        }
    }
}