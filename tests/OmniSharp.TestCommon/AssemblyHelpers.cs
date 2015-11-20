using System;
using System.Reflection;

namespace OmniSharp.TestCommon
{
    public static class AssemblyHelpers
    {
        public static string GetAssemblyLocationFromType(Type type)
        {
            var assembly = type.GetTypeInfo().Assembly;
            var propertyInfo = typeof(Assembly).GetProperty("Location");
            return (string)propertyInfo.GetValue(assembly);
        }
    }
}
