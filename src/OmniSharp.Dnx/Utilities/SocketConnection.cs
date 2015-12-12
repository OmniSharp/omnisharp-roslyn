using System;
using System.Net;
using System.Net.Sockets;
using System.Threading;
using Microsoft.Framework.Logging;

namespace OmniSharp
{
    public class SocketConnection
    {
        public ILogger Logger { get; }
        private string ContextFlag { get; }

        public bool dthConnectSuccess = false;

        public SocketConnection(Socket socket, int port, string contextFlag, ILogger logger)
        {
            Logger = logger;
            ContextFlag = contextFlag;
            Initialize(socket, port);
        }

        private void Initialize(Socket socket, int port)
        {

            socket.NoDelay = true;
            socket.SetSocketOption(SocketOptionLevel.Socket, SocketOptionName.KeepAlive, true);

            SocketAsyncEventArgs connect = new SocketAsyncEventArgs();

            connect.Completed += new EventHandler<SocketAsyncEventArgs>(SocketEventArg_Completed);
            connect.RemoteEndPoint = new IPEndPoint(IPAddress.Loopback, port);
            connect.UserToken = socket;

            Thread.Sleep(500);
            try
            {
                socket.ConnectAsync(connect);
            }
            catch (SocketException)
            {
                // this happens when the DTH isn't listening yet
                Logger.LogError("[SocketConnection]: Socket Connection failed");
            }
        }

        public void SocketEventArg_Completed(object sender, SocketAsyncEventArgs e)
        {
            switch (e.LastOperation)
            {
                case SocketAsyncOperation.Connect:

                    var t1 = DateTime.UtcNow;
                    var dthTimeout = TimeSpan.FromSeconds(30);

                    while (DateTime.UtcNow - t1 < dthTimeout && !dthConnectSuccess && ContextFlag == "DnxProjectSystem")
                    {
                        Thread.Sleep(1000);
                        Logger.LogInformation("Socket State: " + e.SocketError.ToString());

                        if (e.SocketError == SocketError.Success)
                        {
                            dthConnectSuccess = true;
                        }
                    }

                    if (!dthConnectSuccess && ContextFlag == "DnxProjectSystem")
                    {
                        Logger.LogError("Connection Timed Out");
                        throw new SocketException((int)e.SocketError);
                    }
                    break;
            }
        }

    }

}