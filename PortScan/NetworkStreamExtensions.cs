using System.IO;
using System.Net.Sockets;
using System.Threading.Tasks;

namespace PortScan
{
    public static class NetworkStreamExtensions
    {
        public static async Task<Task> WriteAsyncTimeout(this NetworkStream stream, byte[] buffer, int offset,
            int length, int timeout = 3000)
        {
            Task writeTask;
            try
            {
                writeTask = stream.WriteAsync(buffer, offset, length);
            }
            catch
            {
                return Task.FromException(new IOException());
            }
            await Task.WhenAny(writeTask, Task.Delay(timeout));
            return writeTask;
        }
        
        public static async Task<Task> ReadAsyncTimeout(this NetworkStream stream, byte[] buffer, int offset, int length,
            int timeout = 3000)
        {
            Task<int> readTask;
            try
            {
                readTask = stream.ReadAsync(buffer, offset, length);
            }
            catch
            {
                return Task.FromException(new IOException());
            }
            await Task.WhenAny(readTask, Task.Delay(timeout));
            return readTask;
        }
    }
}