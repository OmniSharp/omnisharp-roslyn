using System.Reflection;

namespace OmniSharp.Cake.Polyfill
{
    internal static class AssemblyLoader
    {
        public static Assembly LoadFrom(string assemblyPath)
        {
#if NET46
            return Assembly.LoadFrom(assemblyPath);
#else
            return System.Runtime.Loader.AssemblyLoadContext.Default.LoadFromAssemblyPath(assemblyPath);
#endif
        }
    }
}
