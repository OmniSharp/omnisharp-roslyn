using System;
using System.Diagnostics;
using System.Net;
using System.Net.Sockets;

namespace OmniSharp.DotNetTest.Helpers.DotNetTestManager
{
    public class DotNetTestManager: IDisposable
    {
        private Process _process;
        
        public DotNetTestManager()
        {
            
        }
        
        public void Start()
        {
            
        }
        
        public void Dispose()
        {
            if (_process != null && !_process.HasExited)
            {
                _process.Kill();
            }
        }
        
        private static int FindFreePort()
        {
            using (var socket = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp))
            {
                socket.Bind(new IPEndPoint(IPAddress.Loopback, 0));
                return ((IPEndPoint)socket.LocalEndPoint).Port;
            }
        }
    }
}