using System.Threading;
using System.Threading.Tasks;

namespace OmniSharp.Utilities
{
    public static class TaskExtensions
    {
        public static T WaitAndGetResult<T>(this Task<T> task, CancellationToken cancellationToken)
        {
            task.Wait(cancellationToken);
            return task.Result;
        }
    }
}
