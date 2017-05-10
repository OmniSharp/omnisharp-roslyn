using System.ComponentModel;
using Microsoft.Extensions.CommandLineUtils;

namespace OmniSharp.Host.Internal
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
    }
}
