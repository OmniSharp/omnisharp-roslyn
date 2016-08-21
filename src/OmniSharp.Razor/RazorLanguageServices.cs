using System;
using System.Composition;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Host;
using Microsoft.CodeAnalysis.Host.Mef;
using OmniSharp.Roslyn;
using OmniSharp.Services;

namespace OmniSharp.Razor
{
    [ExportLanguageService(typeof(RazorService), RazorLanguage.Razor), Shared]
    public class RazorService : ILanguageService { }

    [Export(typeof(IHostServicesProvider))]
    [Export(typeof(RazorHostServicesProvider))]
    public class RazorHostServicesProvider : IHostServicesProvider
    {
        public ImmutableArray<Assembly> Assemblies { get; }

        [ImportingConstructor]
        public RazorHostServicesProvider()
        {
            var builder = ImmutableArray.CreateBuilder<Assembly>();

            builder.AddRange(typeof(RazorService).GetTypeInfo().Assembly);

            this.Assemblies = builder.ToImmutable();
        }
    }
}
