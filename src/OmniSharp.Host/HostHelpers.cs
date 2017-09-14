using System;
using Microsoft.Extensions.Logging;
using OmniSharp.Roslyn;
using OmniSharp.Utilities;

namespace OmniSharp
{
    public class HostHelpers
    {
        public static bool LogFilter(string category, LogLevel level, IOmniSharpEnvironment environment)
        {
            if (environment.LogLevel > level)
            {
                return false;
            }

            if (!category.StartsWith("OmniSharp", StringComparison.OrdinalIgnoreCase))
            {
                return false;
            }

            if (string.Equals(category, typeof(WorkspaceInformationService).FullName, StringComparison.OrdinalIgnoreCase))
            {
                return false;
            }

            if (string.Equals(category, typeof(ProjectEventForwarder).FullName, StringComparison.OrdinalIgnoreCase))
            {
                return false;
            }

            return true;
        }

        public static int Start(Func<int> action)
        {
            try
            {
                if (PlatformHelper.IsMono)
                {
                    // Mono uses ThreadPool threads for its async/await implementation.
                    // Ensure we have an acceptable lower limit on the threadpool size to avoid deadlocks and ThreadPool starvation.
                    const int MIN_WORKER_THREADS = 8;

                    int currentWorkerThreads, currentCompletionPortThreads;
                    System.Threading.ThreadPool.GetMinThreads(out currentWorkerThreads, out currentCompletionPortThreads);

                    if (currentWorkerThreads < MIN_WORKER_THREADS)
                    {
                        System.Threading.ThreadPool.SetMinThreads(MIN_WORKER_THREADS, currentCompletionPortThreads);
                    }
                }

                return action();
            }
            catch (Exception e)
            {
                Console.Error.WriteLine(e.ToString());
                return 0xbad;
            }
        }
    }
}
