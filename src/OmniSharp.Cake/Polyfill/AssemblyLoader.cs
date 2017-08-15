using System.Reflection;

namespace OmniSharp.Cake.Polyfill
{
    internal static class AssemblyLoader
    {
        public static Assembly LoadFrom(string assemblyPath)
        {
            return Assembly.LoadFrom(assemblyPath);
        }
    }
}
