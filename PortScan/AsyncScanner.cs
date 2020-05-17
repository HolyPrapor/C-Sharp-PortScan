﻿using System;
using System.Linq;
using System.Net;
using System.Net.NetworkInformation;
using System.Net.Sockets;
using System.Threading.Tasks;

namespace PortScan
{
    public sealed class AsyncPortScanner : ITcpPortScanner
    {
        public Task Scan(IPAddress[] ipAddrs, int[] ports)
        {
            return Task.WhenAll(ipAddrs.Select(async ip =>
            {
                if (await PingAddr(ip) == IPStatus.Success)
                    await Task.WhenAll(ports.Select(port => CheckPortUdp(ip, port)));
            }));
        }

        private static async Task<IPStatus> PingAddr(IPAddress ipAddr, int timeout = 3000)
        {
            Console.WriteLine($"Pinging {ipAddr}");
            using(var ping = new Ping())
            {
                var pingReply = await ping.SendPingAsync(ipAddr, timeout);
                Console.WriteLine($"Pinged {ipAddr}: {pingReply.Status}");
                return pingReply.Status;
            }
        }

        private static async Task CheckPortTcp(IPAddress ipAddr, int port, int timeout = 3000)
        {
            using(var tcpClient = new TcpClient())
            {
                Console.WriteLine($"Checking {ipAddr}:{port}");

                var connectTask = await tcpClient.ConnectAsync(ipAddr, port, timeout);
                PortStatus portStatus;
                switch(connectTask.Status)
                {
                    case TaskStatus.RanToCompletion:
                        portStatus = PortStatus.OPEN;
                        break;
                    case TaskStatus.Faulted:
                        portStatus = PortStatus.CLOSED;
                        break;
                    default:
                        portStatus = PortStatus.FILTERED;
                        break;
                }
                Console.WriteLine($"Checked {ipAddr}:{port} - {portStatus}");

                if (portStatus == PortStatus.OPEN)
                {
                    //TODO: Handle protocol detection
                    // var data = new byte[256];
                    // NetworkStream stream = tcpClient.GetStream();
                    //
                    // // Send the message to the connected TcpServer.
                    // stream.Write(data, 0, data.Length);
                    //
                    // // Receive the TcpServer.response.
                    //
                    // // Buffer to store the response bytes.
                    // data = new byte[256];
                    //
                    // // String to store the response ASCII representation.
                    // String responseData = String.Empty;
                    //
                    // // Read the first batch of the TcpServer response bytes.
                    // Int32 bytes = stream.Read(data, 0, data.Length);
                    // responseData = System.Text.Encoding.ASCII.GetString(data, 0, bytes);
                    // Console.WriteLine("Received: {0}", responseData);
                    //
                    // // Close everything.
                    // stream.Close();
                }
            }
        }

        private static async Task CheckPortUdp(IPAddress ipAddr, int port, int timeout = 3000)
        {
            using (var udpClient = new UdpClient())
            {
                Console.WriteLine($"Checking {ipAddr}:{port}");

                var connectTask = await udpClient.ConnectAsync(ipAddr, port, timeout);
                PortStatus portStatus;
                if (connectTask.Exception != null)
                {
                    if (connectTask.Exception.InnerExceptions
                        .Where(x => x is SocketException)
                        .Any(x => ((SocketException) x).ErrorCode == 10054)) //WSAECONNRESET
                        portStatus = PortStatus.CLOSED;
                    else if (connectTask.Exception.InnerExceptions.Any(x => x is TimeoutException))
                        portStatus = PortStatus.OPEN;
                    else
                        portStatus = PortStatus.FILTERED;
                }
                else
                {
                    portStatus = PortStatus.OPEN;
                    //TODO: Analyze protocol
                }
                
                Console.WriteLine($"Checked {ipAddr}:{port} - {portStatus}");
            }
        }
    }
}