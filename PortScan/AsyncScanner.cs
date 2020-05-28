﻿using System;
 using System.Globalization;
 using System.IO;
 using System.Linq;
 using System.Net;
 using System.Net.NetworkInformation;
 using System.Net.Sockets;
 using System.Text;
 using System.Threading.Tasks;

 namespace PortScan
{
    public sealed class AsyncPortScanner : IPortScanner
    {
        public static byte[] PacketToSend;

        static AsyncPortScanner()
        {
            var hex =
                "130000000000000000000000000000000000000000000000000000000000000000000000000000006f89e91ab6d53bd3";
            PacketToSend = Enumerable.Range(0, hex.Length)
                .Where(x => x % 2 == 0)
                .Select(x => Convert.ToByte(hex.Substring(x, 2), 16))
                .ToArray();
        }
        
        public Task Scan(IPAddress[] ipAddrs, int[] ports, bool scanTcp, bool scanUdp)
        {
            return Task.WhenAll(ipAddrs.Select(async ip =>
            {
                if (await PingAddr(ip) == IPStatus.Success)
                {
                    if(scanTcp)
                        await Task.WhenAll(ports.Select(port => CheckPortTcp(ip, port)));
                    if(scanUdp)
                        await Task.WhenAll(ports.Select(port => CheckPortUdp(ip, port)));
                }
            }));
        }

        private static async Task<IPStatus> PingAddr(IPAddress ipAddr, int timeout = 3000)
        {
            using(var ping = new Ping())
            {
                var pingReply = await ping.SendPingAsync(ipAddr, timeout);
                return pingReply.Status;
            }
        }

        private static async Task CheckPortTcp(IPAddress ipAddr, int port, int timeout = 2000)
        {
            using(var tcpClient = new TcpClient())
            {
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

                ProtocolType? protocolType = null;
                if (portStatus == PortStatus.OPEN)
                {
                    var data = new byte[256];
                    using (var stream = tcpClient.GetStream())
                    {
                        stream.ReadTimeout = timeout;
                        stream.WriteTimeout = timeout;
                        var writeTask = stream.WriteAsync(PacketToSend, 0, PacketToSend.Length);
                        var readTask = stream.ReadAsync(data, 0, data.Length);

                        try
                        {
                            await writeTask;
                        }
                        catch (Exception e)
                        {
                            //Console.WriteLine(e.Message);
                        }
                        
                        if (writeTask.Status == TaskStatus.RanToCompletion)
                        {
                            try
                            {
                                await readTask;
                            }
                            catch(Exception e)
                            {
                                //Console.WriteLine(e.Message);
                            }
                            
                            if(readTask.Status == TaskStatus.RanToCompletion)
                            {
                                var length = readTask.Result;
                                data = data.Take(length).ToArray();
                                protocolType = ProtocolCheckingUtils.DetectProtocol(data);
                            }
                        }
                    }
                    Console.WriteLine($"TCP {port} {protocolType}");
                }
            }
        }

        private static async Task CheckPortUdp(IPAddress ipAddr, int port, int timeout = 2000)
        {
            using (var udpClient = new UdpClient())
            {
                var connectTask = udpClient.Client.ConnectAsync(ipAddr, port);
                PortStatus portStatus;
                ProtocolType? protocolType = null;
                await Task.WhenAny(Task.Delay(timeout), connectTask);
                if (connectTask.IsCompleted &&
                    connectTask.Exception != null && connectTask.Exception.InnerExceptions
                        .Where(x => x is SocketException)
                        .Any(x => ((SocketException) x).ErrorCode == 10054)) //WSAECONNRESET
                    portStatus = PortStatus.CLOSED;
                else
                    portStatus = PortStatus.OPEN;

                if (portStatus == PortStatus.OPEN)
                {
                    var writeTask = udpClient.SendAsync(PacketToSend, PacketToSend.Length);
                    var readTask = udpClient.ReceiveAsync();
                    try
                    {
                        await Task.WhenAny(writeTask, Task.Delay(timeout));
                    }
                    catch (Exception e)
                    {
                        //Console.WriteLine(e);
                    }

                    if (writeTask.Status == TaskStatus.RanToCompletion)
                    {
                        try
                        {
                            await Task.WhenAny(readTask, Task.Delay(timeout));
                        }
                        catch (Exception e)
                        {
                            //Console.WriteLine(e);
                        }

                        if (readTask.Status == TaskStatus.RanToCompletion)
                            protocolType = ProtocolCheckingUtils.DetectProtocol(readTask.Result.Buffer);
                        else
                            return;
                    }
                    else
                        return;
                    
                    Console.WriteLine($"UDP {port} {protocolType}");
                } 
            }
        }
    }

    public static class ProtocolCheckingUtils
    {
        public static ProtocolType? DetectProtocol(byte[] data)
        {
            var responseData = Encoding.ASCII.GetString(data, 0, data.Length).ToLower();
            if (responseData.Contains("html"))
                return ProtocolType.HTTP;
            if (responseData.Contains("smtp"))
                return ProtocolType.SMTP;
            if (responseData.Contains("pop3"))
                return ProtocolType.POP3;
            if (responseData.Contains("imap"))
                return ProtocolType.IMAP;
            if (IsDnsResponse(data))
                return ProtocolType.DNS;
            if (IsNtpResponse(data))
                return ProtocolType.NTP;
            return null;
        }
        
        public static bool IsDnsResponse(byte[] response)
        {
            return response[0] == AsyncPortScanner.PacketToSend[0] &&
                   response[1] == AsyncPortScanner.PacketToSend[1]
                   && (response[3] & 1) == 1;
        }

        public static bool IsNtpResponse(byte[] response)
        {
            if (response.Length <= 39)
                return false;
            for(var i = 0; i < 8; i++)
                if (AsyncPortScanner.PacketToSend[40 + i] != response[24 + i])
                    return false;
            return (response[0] & 7) == 4 &&
                   ((response[0] >> 3) & 7) == 2;
        }
    }
}