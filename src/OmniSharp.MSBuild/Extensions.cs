using System.Collections.Generic;
using System.Collections.Immutable;
using Microsoft.Extensions.Logging;

namespace OmniSharp.MSBuild
{
    internal static class Extensions
    {
        public static void AddPropertyOverride(
            this Dictionary<string, string> properties,
            string propertyName,
            string userOverrideValue,
            ImmutableDictionary<string, string> propertyOverrides,
            ILogger logger)
        {
            var overrideValue = propertyOverrides.GetValueOrDefault(propertyName);

            if (!string.IsNullOrEmpty(userOverrideValue))
            {
                // If the user set the option, we should use that.
                properties.Add(propertyName, userOverrideValue);
                logger.LogDebug($"'{propertyName}' set to '{userOverrideValue}' (user override)");
            }
            else if (!string.IsNullOrEmpty(overrideValue))
            {
                // If we have a custom environment value, we should use that.
                properties.Add(propertyName, overrideValue);
                logger.LogDebug($"'{propertyName}' set to '{overrideValue}'");
            }
        }
    }
}
