using System;
using System.Diagnostics;
using System.IO;
using System.Net;
using System.Net.Sockets;

namespace Meadow.DebugAdapterServer.DebuggerTransport
{
    public class TcpServerDebuggerTransport : IDebuggerTransport
    {
        public Stream InputStream { get; private set; }
        public Stream OutputStream { get; private set; }

        public TcpServerDebuggerTransport(int port = 0, IPAddress ipAddr = null)
        {
            if (ipAddr == null)
            {
                ipAddr = IPAddress.Loopback;
            }

            var tcpListener = new TcpListener(ipAddr, port);
            tcpListener.Start();
            
            var listenAddress = ((IPEndPoint)tcpListener.LocalEndpoint).Address;
            var listenPort = ((IPEndPoint)tcpListener.LocalEndpoint).Port;
            Console.WriteLine($"Waiting for connection @ {listenAddress}:{listenPort}");

            var socket = tcpListener.AcceptSocket();
            Console.WriteLine("Connected");

            var stream = new NetworkStream(socket);
            InputStream = stream;
            OutputStream = stream;
        }

        public void Dispose()
        {
        }
    }
}