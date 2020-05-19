using System;
using System.Net;
using System.Net.Sockets;
using System.Threading.Tasks;

namespace PortScan
{
    public static class UdpClientExtensions
    {
        public static async Task<Task> ConnectAsync(this UdpClient udpClient, IPAddress ipAddr, int port,
            int timeout = 3000)
        {
            Task receiveTask;
            try
            {
                udpClient.Connect(ipAddr, port);
                var sendTask = udpClient.SendAsync(new byte[256], 256);
                await sendTask;
                receiveTask = udpClient.ReceiveAsync();
                await receiveTask.ThrowAfterTimeout(timeout);
            }
            catch (TimeoutException)
            {
                return Task.FromException(new TimeoutException());
            }
            catch
            {
                return Task.FromException(new SocketException());
            }
            return receiveTask;
        }
    }
}