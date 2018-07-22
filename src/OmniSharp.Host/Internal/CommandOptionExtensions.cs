using System;
using System.ComponentModel;
using McMaster.Extensions.CommandLineUtils;

namespace OmniSharp.Internal
{
    public static class CommandOptionExtensions
    {
        public static T GetValueOrDefault<T>(this CommandOption opt, T defaultValue)
        {
            if (opt.HasValue())
            {
                return (T)TypeDescriptor.GetConverter(typeof(T)).ConvertFrom(opt.Value());
            }

            return defaultValue;
        }

        public static bool GetValueOrDefault(this CommandOption opt, bool defaultValue)
        {
            return opt.Value()?.Equals("on", StringComparison.OrdinalIgnoreCase) == true || defaultValue;
        }
    }
}
