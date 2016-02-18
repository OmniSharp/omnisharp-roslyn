using System.Reflection;
using OmniSharp.Services;

namespace OmniSharp.Host.Loader
{
    public class OmnisharpAssemblyLoader : IOmnisharpAssemblyLoader
    {
        public OmnisharpAssemblyLoader() { }

        public virtual Assembly Load(AssemblyName name)
        {
            return Assembly.Load(name);
        }
    }
}
