using System;
using System.Reflection;

namespace TestUtility
{
    public static class AssemblyHelpers
    {
        public const string CorLibName = "mscorlib";

        public static Assembly FromType(Type type)
        {
            return type.GetTypeInfo().Assembly;
        }

        public static Assembly FromName(string assemblyName)
        {
            return FromName(new AssemblyName(assemblyName));
        }

        public static Assembly FromName(AssemblyName assemblyName)
        {
            try
            {
                return Assembly.Load(assemblyName);
            }
            catch
            {
                return null;
            }
        }
    }
}
