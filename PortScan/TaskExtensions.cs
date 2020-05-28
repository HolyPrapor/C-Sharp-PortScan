using System;
using System.Threading.Tasks;

namespace PortScan
{
    public static class TaskExtensions
    {
        public static async Task<Task> ThrowAfterTimeout(this Task task, int timeout)
        {
            var delayTask = Task.Delay(timeout);
            var result = await Task.WhenAny(task, delayTask);
            if(result == delayTask)
                throw new TimeoutException();
            return result;
        }
        
        public static async Task<T> ThrowAfterTimeout<T>(this Task<T> task, int timeout)
        {
            var delayTask = Task.Delay(timeout);
            var result = await Task.WhenAny(task, delayTask);
            if(result == delayTask)
                throw new TimeoutException();
            return task.Result;
        }
    }
}