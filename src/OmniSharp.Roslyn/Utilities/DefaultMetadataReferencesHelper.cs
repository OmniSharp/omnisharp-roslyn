using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;

namespace OmniSharp.Roslyn.Utilities
{
    public static class DefaultMetadataReferenceHelper
    {
        public static IEnumerable<string> GetDefaultMetadataReferenceLocations()
        {
            var assemblies = new[]
            {
                typeof(object).GetTypeInfo().Assembly,
                typeof(Enumerable).GetTypeInfo().Assembly,
                typeof(Stack<>).GetTypeInfo().Assembly,
                typeof(Lazy<,>).GetTypeInfo().Assembly,
                FromName("System.Runtime"),
                FromName("mscorlib")
            };

           return assemblies
                .Where(a => a != null)
                .Select(a => a.Location)
                .Distinct();

            Assembly FromName(string assemblyName)
            {
                try
                {
                    return Assembly.Load(new AssemblyName(assemblyName));
                }
                catch
                {
                    return null;
                }
            }
        }
    }
}
