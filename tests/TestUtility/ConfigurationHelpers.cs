using System.Collections.Generic;
using System.Reactive;
using System.Reactive.Linq;
using System.Reactive.Threading.Tasks;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Options;
using Microsoft.Extensions.Primitives;

namespace TestUtility
{
    public static class ConfigurationHelpers
    {
        public static IConfiguration ToConfiguration(this IEnumerable<KeyValuePair<string, string>> configurationData)
        {
            return new ConfigurationBuilder().AddInMemoryCollection(configurationData).Build();
        }

        public static Task WaitForChange(this IConfiguration configuration, CancellationToken cancellationToken)
        {
            return Observable.Create<Unit>(observer =>
            {
                var reloadToken = configuration.GetReloadToken();
                return reloadToken.RegisterChangeCallback(_ =>
                {
                    observer.OnNext(Unit.Default);
                    observer.OnCompleted();
                }, Unit.Default);
            }).ToTask(cancellationToken);
        }

        public static Task WaitForChange<T>(this IOptionsMonitor<T> options, CancellationToken cancellationToken)
        {
            return Observable.Create<Unit>(observer =>
            {
                return options.OnChange(_ =>
                {
                    observer.OnNext(Unit.Default);
                    observer.OnCompleted();

                });
            }).ToTask(cancellationToken);
        }
    }
}
