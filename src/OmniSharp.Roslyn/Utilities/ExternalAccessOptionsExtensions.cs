using System;
using System.Reflection;

namespace OmniSharp.Utilities
{
    public static class ExternalAccessOptionsExtensions
    {
        public static T Create<T>()
            => Activator.CreateInstance<T>();

        public static T WithProperty<T, TValue>(this T options, string propertyName, TValue value)
        {
            var property = typeof(T).GetProperty(propertyName, BindingFlags.Instance | BindingFlags.Public)
                ?? throw new MissingMemberException(typeof(T).FullName, propertyName);

            object boxedOptions = options;
            property.SetValue(boxedOptions, value);
            return (T)boxedOptions;
        }
    }
}
