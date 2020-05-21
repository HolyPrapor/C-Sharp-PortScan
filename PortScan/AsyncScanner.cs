﻿using System;
 using System.Globalization;
 using System.IO;
 using System.Linq;
using System.Net;
using System.Net.NetworkInformation;
using System.Net.Sockets;
using System.Threading.Tasks;

namespace PortScan
{
    public static class ActualDay
    {
        public static DateTime CurrentDateTime;
        static ActualDay()
        {
            try
            {
                var client = new TcpClient("time.nist.gov", 13);
                using (var streamReader = new StreamReader(client.GetStream()))
                {
                    var response = streamReader.ReadToEnd();
                    var utcDateTimeString = response.Substring(7, 17);
                    CurrentDateTime = DateTime.ParseExact(utcDateTimeString, "yy-MM-dd HH:mm:ss",
                        CultureInfo.InvariantCulture, DateTimeStyles.AssumeUniversal);
                }
            }
            catch
            {
                CurrentDateTime = new DateTime(1900, 1, 1);
            }
        }
    }
    
    
    public sealed class AsyncPortScanner : IPortScanner
    {
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

        private static async Task CheckPortTcp(IPAddress ipAddr, int port, int timeout = 3000)
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
                        if ((await stream.WriteAsyncTimeout(data, 0, data.Length)).IsCompleted &&
                            (await stream.ReadAsyncTimeout(data, 0, data.Length)).IsCompleted)
                        {
                            var responseData = System.Text.Encoding.ASCII.GetString(data, 0,
                                data.Length).ToLower();
                            if (responseData.Contains("html"))
                                protocolType = ProtocolType.HTTP;
                            else if (responseData.Contains("smtp"))
                                protocolType = ProtocolType.SMTP;
                            else if (responseData.Contains("pop3"))
                                protocolType = ProtocolType.POP3;
                            else if (responseData.Contains("imap"))
                                protocolType = ProtocolType.IMAP;
                        }
                    }
                    Console.WriteLine($"TCP {port} {protocolType}");
                }
            }
        }

        private static async Task CheckPortUdp(IPAddress ipAddr, int port, int timeout = 3000)
        {
            using (var udpClient = new UdpClient())
            {
                //Console.WriteLine($"Checking {ipAddr}:{port}");

                var connectTask = await udpClient.ConnectAsync(ipAddr, port, timeout);
                PortStatus portStatus;
                ProtocolType? protocolType = null;
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
                    Console.WriteLine($"OPENED {port}");
                    portStatus = PortStatus.OPEN;
                    
                    if (await UdpProtocolChecker.IsNtp(udpClient))
                        protocolType = ProtocolType.NTP;
                    else if (await UdpProtocolChecker.IsDns(udpClient))
                        protocolType = ProtocolType.DNS;
                }
                
                if (portStatus == PortStatus.OPEN) 
                    Console.WriteLine($"UDP {port} {protocolType}");
            }
        }
    }

    public static class UdpProtocolChecker
    {
        private static DateTime GetNetworkTime(byte[] ntpData)
        {
            var intPart = (ulong)ntpData[40] << 24 | (ulong)ntpData[41] << 16 | (ulong)ntpData[42] << 8 | ntpData[43];
            var fractPart = (ulong)ntpData[44] << 24 | (ulong)ntpData[45] << 16 | (ulong)ntpData[46] << 8 | ntpData[47];

            var milliseconds = intPart * 1000 + fractPart * 1000 / 0x100000000L;
            var networkDateTime = new DateTime(1900, 1, 1).AddMilliseconds((long)milliseconds);

            return networkDateTime;
        }
        
        public static async Task<bool> IsNtp(UdpClient udpClient)
        {
            var ntpData = new byte[48];

            //Setting the Leap Indicator, Version Number and Mode values
            ntpData[0] = 0x1B; //LI = 0 (no warning), VN = 3 (IPv4 only), Mode = 3 (Client Mode)
            var responseTask = udpClient.ReceiveAsync();

            var responseData = await udpClient.SendAsync(ntpData, ntpData.Length).ThrowAfterTimeout(3000)
                .ContinueWith(x => responseTask.ThrowAfterTimeout(3000)).Result;

            return responseData == responseTask && GetNetworkTime(responseTask.Result.Buffer)
                .ToLocalTime().Date.Equals(ActualDay.CurrentDateTime.Date);
        }

        public static async Task<bool> IsDns(UdpClient udpClient)
        {
            var dnsData = new byte[50];
            var randomBytes = BitConverter.GetBytes(new Random().Next());
            for (var i = 0; i < randomBytes.Length; i++)
                dnsData[i] = randomBytes[i];
            var responseTask = udpClient.ReceiveAsync();
            
            var responseData = await udpClient.SendAsync(dnsData, dnsData.Length).ThrowAfterTimeout(3000)
                .ContinueWith(x => responseTask.ThrowAfterTimeout(3000)).Result;

            return responseData == responseTask && responseTask.Result.Buffer.Length > 11 && 
                   responseTask.Result.Buffer[0] == dnsData[0] &&
                   responseTask.Result.Buffer[1] == dnsData[1]
                && (responseTask.Result.Buffer[3] & 1) == 1;
        }
    }
}