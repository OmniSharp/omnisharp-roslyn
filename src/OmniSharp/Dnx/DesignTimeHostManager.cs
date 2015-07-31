using System;
using System.Diagnostics;
using System.IO;
using System.Net;
using System.Net.Sockets;
using System.Threading;
using Microsoft.Framework.Logging;

namespace OmniSharp.Dnx
{
    public class DesignTimeHostManager
    {
        private readonly ILogger _logger;
        private readonly DnxPaths _paths;
        private readonly object _processLock = new object();
        private Process _designTimeHostProcess;
        private bool _stopped;

        public DesignTimeHostManager(ILoggerFactory loggerFactory, DnxPaths paths)
        {
            _logger = loggerFactory.CreateLogger<DesignTimeHostManager>();
            _paths = paths;
        }

        public TimeSpan DelayBeforeRestart { get; set; }

        public void Start(string hostId, Action<int> onConnected)
        {
            lock (_processLock)
            {
                if (_stopped)
                {
                    return;
                }

                int port = GetFreePort();

                var dthPath = Path.Combine(_paths.RuntimePath.Value, "bin", "lib", "Microsoft.Dnx.DesignTimeHost", "Microsoft.Dnx.DesignTimeHost.dll");
                // TODO: This is for backcompat. Once the dust settles, and MS.Framework.DTH goes away, remove this.
                if (!File.Exists(dthPath))
                  dthPath = Path.Combine(_paths.RuntimePath.Value, "bin", "lib", "Microsoft.Framework.DesignTimeHost", "Microsoft.Framework.DesignTimeHost.dll");

                var psi = new ProcessStartInfo
                {
                    FileName = _paths.Dnx ?? _paths.Klr,
                    CreateNoWindow = true,
                    UseShellExecute = false,
                    RedirectStandardError = true,
                    Arguments = string.Format(@"""{0}"" {1} {2} {3}",
                                              dthPath,
                                              port,
                                              Process.GetCurrentProcess().Id,
                                              hostId),
                };

#if DNX451
                psi.EnvironmentVariables["KRE_APPBASE"] = Directory.GetCurrentDirectory();
                psi.EnvironmentVariables["DNX_APPBASE"] = Directory.GetCurrentDirectory();
#else
                psi.Environment["KRE_APPBASE"] = Directory.GetCurrentDirectory();
                psi.Environment["DNX_APPBASE"] = Directory.GetCurrentDirectory();
#endif

                _logger.LogVerbose(psi.FileName + " " + psi.Arguments);

                _designTimeHostProcess = Process.Start(psi);

                // Wait a little bit for it to conncet before firing the callback
                using (var socket = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp))
                {
                    var t1 = DateTime.UtcNow;
                    var dthTimeout = TimeSpan.FromSeconds(10);
                    while (!socket.Connected && DateTime.UtcNow - t1 < dthTimeout)
                    {
                        Thread.Sleep(500);
                        try
                        {
                            socket.Connect(new IPEndPoint(IPAddress.Loopback, port));
                        }
                        catch (SocketException)
                        {
                            // this happens when the DTH isn't listening yet
                        }
                    }

                    if (!socket.Connected)
                    {
                        // reached timeout
                        _logger.LogError("Failed to launch DesignTimeHost in a timely fashion.");
                        return;
                    }
                }

                if (_designTimeHostProcess.HasExited)
                {
                    // REVIEW: Should we quit here or retry?
                    _logger.LogError(string.Format("Failed to launch DesignTimeHost. Process exited with code {0}.", _designTimeHostProcess.ExitCode));
                    return;
                }

                _logger.LogInformation(string.Format("Running DesignTimeHost on port {0}, with PID {1}", port, _designTimeHostProcess.Id));

                _designTimeHostProcess.EnableRaisingEvents = true;
                _designTimeHostProcess.OnExit(() =>
                {
                    _logger.LogWarning("Design time host process ended");

                    Start(hostId, onConnected);
                });

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
                    _logger.LogInformation("Shutting down DesignTimeHost");

                    _designTimeHostProcess.KillAll();
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
