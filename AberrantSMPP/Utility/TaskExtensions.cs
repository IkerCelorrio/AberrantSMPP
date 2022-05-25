using System;
using System.Threading;
using System.Threading.Tasks;

namespace AberrantSMPP.Utility
{
    public static class TaskExtensions
    {
        public static Task WithCancellation(this Task task, CancellationToken token)
        {
            if (task.IsCompleted) // fast-path optimization
                return task;
            
            return task.ContinueWith(t => t.GetAwaiter().GetResult(), token);
        }
        public static Task<T> WithCancellation<T>(this Task<T> task, CancellationToken token)
        {
            if (task.IsCompleted) // fast-path optimization
                return task;
            
            return task.ContinueWith(t => t.GetAwaiter().GetResult(), token);
        }
    }
}