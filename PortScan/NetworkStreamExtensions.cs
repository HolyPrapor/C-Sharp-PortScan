using System;
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
                await Task.WhenAny(writeTask, Task.Delay(timeout));
            }
            catch
            {
                return Task.FromException(new IOException());
            }
            return writeTask;
        }
        
        public static async Task<int> ReadAsyncTimeout(this NetworkStream stream, byte[] buffer, int offset, int length,
            int timeout = 3000)
        {
            var readTask = stream.ReadAsync(buffer, offset, length);
            await Task.WhenAny(readTask, Task.Delay(timeout));
            if (readTask.IsCompleted)
                return readTask.Result;
            throw new TimeoutException();
        }
    }
}