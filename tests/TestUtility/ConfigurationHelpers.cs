using System;
using System.Collections.Generic;
using System.Linq;
using System.Reactive.Linq;
using Microsoft.Extensions.Configuration;

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
