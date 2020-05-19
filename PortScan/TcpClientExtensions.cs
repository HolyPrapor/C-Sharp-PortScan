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
            }
            catch
            {
                return Task.FromException(new SocketException());
            }
            await connectTask.ThrowAfterTimeout(timeout);
            return connectTask;
        }
    }
}