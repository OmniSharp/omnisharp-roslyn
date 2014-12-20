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

        private readonly object _processLock = new object();
        private Process _designTimeHostProcess;
        private bool _stopped;

        public DesignTimeHostManager(ILoggerFactory loggerFactory)
        {
            _logger = loggerFactory.Create<DesignTimeHostManager>();

            DelayBeforeRestart = TimeSpan.FromSeconds(1000);
        }

        public TimeSpan DelayBeforeRestart { get; set; }

        public void Start(string runtimePath, string hostId, Action<int> onConnected)
        {
            lock (_processLock)
            {
                if (_stopped)
                {
                    return;
                }

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

                _designTimeHostProcess = Process.Start(psi);

                // Wait a little bit for it to conncet before firing the callback
                Thread.Sleep(1000);

                if (_designTimeHostProcess.HasExited)
                {
                    // REVIEW: Should we quit here or retry?
                    _logger.WriteError(string.Format("Failed to launch DesignTimeHost. Process exited with code {0}.", _designTimeHostProcess.ExitCode));
                    return;
                }

                _logger.WriteInformation(string.Format("Running DesignTimeHost on port {0}, with PID {1}", port, _designTimeHostProcess.Id));

                _designTimeHostProcess.EnableRaisingEvents = true;
                _designTimeHostProcess.Exited += (sender, e) =>
                {
                    _logger.WriteWarning("Design time host process ended");

                    Thread.Sleep(DelayBeforeRestart);

                    Start(runtimePath, hostId, onConnected);
                };

                onConnected(port);
            }
        }

        public void Stop()
        {
            lock (_processLock)
            {
                if (_stopped)
                {
                    return;
                }

                _stopped = true;

                if (_designTimeHostProcess != null)
                {
                    _logger.WriteInformation("Shutting down DesignTimeHost");

                    _designTimeHostProcess.Kill();
                    _designTimeHostProcess.WaitForExit(1000);
                    _designTimeHostProcess = null;
                }
            }
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