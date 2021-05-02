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
    }
}
