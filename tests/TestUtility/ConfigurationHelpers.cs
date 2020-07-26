using System;
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
            var cb = new ConfigurationBuilder();
            if (configurationData != null) cb.AddInMemoryCollection(configurationData);
            return cb.Build();
        }

        public static Task WaitForChange(this IConfiguration configuration, CancellationToken cancellationToken)
        {
            if (configuration == null) throw new ArgumentNullException(nameof(configuration));
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
            if (options == null) throw new ArgumentNullException(nameof(options));
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
