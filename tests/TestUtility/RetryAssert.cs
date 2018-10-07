using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace TestUtility
{
    public static class RetryAssert
    {
        // On 99% cases asserts should not require retry, however build systems are very slow sometimes with unpredictable ways.
        public static Task On<TException>(Func<Task> action, int maxAttemptCount = 5) where TException : Exception
        {
            var exceptions = new List<Exception>();

            for (int attempted = 0; attempted < maxAttemptCount; attempted++)
            {
                try
                {
                    if (attempted > 0)
                    {
                        Thread.Sleep(TimeSpan.FromSeconds(10));
                    }
                    return action();
                }
                catch (TException ex)
                {
                    exceptions.Add(ex);
                }
            }
            throw new AggregateException(exceptions);
        }
    }
}