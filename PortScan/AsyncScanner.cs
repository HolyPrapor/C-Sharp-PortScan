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
                    await Task.WhenAll(ports.Select(port => CheckPort(ip, port)));
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

        private static async Task CheckPort(IPAddress ipAddr, int port, int timeout = 3000)
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
                Console.WriteLine(tcpClient.GetStream());
            }
        }
    }
}