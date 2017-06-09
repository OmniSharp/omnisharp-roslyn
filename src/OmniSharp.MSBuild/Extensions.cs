using System.Collections.Generic;
using Microsoft.Extensions.Logging;

namespace OmniSharp.MSBuild
{
    internal static class Extensions
    {
        public static void AddPropertyIfNeeded(this Dictionary<string, string> properties, ILogger logger, string name, string userOptionValue, string environmentValue)
        {
            if (!string.IsNullOrWhiteSpace(userOptionValue))
            {
                // If the user set the option, we should use that.
                properties.Add(name, userOptionValue);
            }
            else if (!string.IsNullOrWhiteSpace(environmentValue))
            {
                // If we have a custom environment value, we should use that.
                properties.Add(name, environmentValue);
            }

            if (properties.TryGetValue(name, out var value))
            {
                logger.LogDebug($"Using {name}: {value}");
            }
        }
    }
}
