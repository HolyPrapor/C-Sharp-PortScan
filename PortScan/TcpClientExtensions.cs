using System;
using System.Net;
using System.Net.Sockets;
using System.Threading.Tasks;

namespace PortScan
{
    public static class TcpClientExtensions
    {
        public static async Task<Task> ConnectAsync(this TcpClient tcpClient, IPAddress ipAddr, int port, int timeout = 3000)
        {
            Task connectTask;
            try
            {
                connectTask = tcpClient.ConnectAsync(ipAddr, port);
                await connectTask.ThrowAfterTimeout(timeout);
            }
            catch (SocketException)
            {
                return Task.FromException(new SocketException());
            }
            catch (TimeoutException)
            {
                return Task.FromException(new TimeoutException());
            }
            return connectTask;
        }
    }
}