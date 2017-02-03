using System;
using System.Reflection;

namespace TestUtility
{
    public static class AssemblyHelpers
    {
#if NETSTANDARD1_6
        public const string CorLibName = "System.Private.CoreLib";
#else
        public const string CorLibName = "mscorlib";
#endif

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
