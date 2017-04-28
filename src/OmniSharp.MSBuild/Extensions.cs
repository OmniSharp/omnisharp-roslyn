using System.Collections.Generic;

namespace OmniSharp.MSBuild
{
    internal static class Extensions
    {
        public static void AddPropertyIfNeeded(this Dictionary<string, string> properties, string name, string userOptionValue, string environmentValue)
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
        }
    }
}
