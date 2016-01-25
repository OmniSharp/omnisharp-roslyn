using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Composition;
using System.Diagnostics;
using System.Linq;
using System.Reflection;
using Microsoft.CodeAnalysis.Host.Mef;
using OmniSharp.Services;

namespace OmniSharp
{
    [Export]
    public class HostServicesBuilder
    {
        private readonly ImmutableArray<Assembly> _assemblies;

        [ImportingConstructor]
        public HostServicesBuilder([ImportMany] IEnumerable<ICodeActionProvider> codeActionProviders)
        {
            var assemblies = MefHostServices.DefaultAssemblies;
            assemblies = assemblies.AddRange(codeActionProviders.SelectMany(x => x.Assemblies));

            _assemblies = assemblies;
        }

        public MefHostServices GetHostServices()
        {
            try
            {
                return MefHostServices.Create(_assemblies);
            }
            catch (ReflectionTypeLoadException ex)
            {
                var exceptions = ex.LoaderExceptions;
                foreach (var e in exceptions)
                {
                    Console.WriteLine(e.Message);
                }

                throw;
            }
        }
    }
}
