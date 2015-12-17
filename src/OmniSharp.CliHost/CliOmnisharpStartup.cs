using System.Collections.Generic;
using System.Reflection;
using Microsoft.Extensions.PlatformAbstractions;
using OmniSharp.Services;

namespace OmniSharp.CliHost
{
    public class CliOmnisharpStartup : Startup
    {
        public CliOmnisharpStartup(IApplicationEnvironment applicationEnvironment,
                                   IOmnisharpEnvironment omnisharpEnvironment)
            : base(applicationEnvironment, omnisharpEnvironment)
        {
        }

        protected override IEnumerable<Assembly> LoadAssemblies()
        {
            return new Assembly[]
            {
                Assembly.Load(new AssemblyName("OmniSharp.Abstractions")),
                Assembly.Load(new AssemblyName("OmniSharp.DotNet")),
                Assembly.Load(new AssemblyName("OmniSharp.Plugins")),
                Assembly.Load(new AssemblyName("OmniSharp.Roslyn")),
                Assembly.Load(new AssemblyName("OmniSharp.Roslyn.CSharp")),
                Assembly.Load(new AssemblyName("OmniSharp.Stdio"))
            };
        }
    }
}
