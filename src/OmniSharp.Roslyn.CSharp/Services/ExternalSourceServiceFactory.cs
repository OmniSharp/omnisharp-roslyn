using Microsoft.CodeAnalysis.Host;
using Microsoft.Extensions.Logging;
using OmniSharp.Options;
using OmniSharp.Roslyn.CSharp.Services.Decompilation;
using OmniSharp.Services;
using System;
using System.Collections.Generic;
using System.Composition;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace OmniSharp.Roslyn.CSharp.Services
{
    [Export(typeof(ExternalSourceServiceFactory)), Shared]
    public class ExternalSourceServiceFactory
    {
        private MetadataExternalSourceService _metadataExternalSourceService;
        private DecompilationExternalSourceService _decompilationExternalSourceService;
        private readonly IAssemblyLoader _assemblyLoader;
        private readonly ILoggerFactory _loggerFactory;
        private static object padlock = new object();

        [ImportingConstructor]
        public ExternalSourceServiceFactory(IAssemblyLoader assemblyLoader, ILoggerFactory loggerFactory)
        {
            _assemblyLoader = assemblyLoader;
            _loggerFactory = loggerFactory;
        }

        public IExternalSourceService Create(OmniSharpOptions omniSharpOptions, HostLanguageServices hostLanguageServices)
        {
            var enableDecompilationSupport = omniSharpOptions.RoslynExtensionsOptions.EnableDecompilationSupport;

            if (enableDecompilationSupport)
            {
                if (_decompilationExternalSourceService == null)
                {
                    lock (padlock)
                    {
                        if (_decompilationExternalSourceService == null)
                        {
                            _decompilationExternalSourceService = new DecompilationExternalSourceService(_assemblyLoader, _loggerFactory, hostLanguageServices);
                        }
                    }
                }

                return _decompilationExternalSourceService;
            }

            if (_metadataExternalSourceService == null)
            {
                lock (padlock)
                {
                    if (_metadataExternalSourceService == null)
                    {
                        _metadataExternalSourceService = new MetadataExternalSourceService(_assemblyLoader);
                    }
                }
            }

            return _metadataExternalSourceService;
        }

        public CancellationToken CreateCancellationToken(OmniSharpOptions omniSharpOptions, int timeout)
        {
            var enableDecompilationSupport = omniSharpOptions.RoslynExtensionsOptions.EnableDecompilationSupport;
            // since decompilation is slower, use a larger cancellation time (default is 2s per request)
            var cancellationTimeout = enableDecompilationSupport
                ? timeout <= 10000 ? 10000 : timeout // minimum 10s for decompilation
                : timeout; // request defined for metadata

            return new CancellationTokenSource(timeout).Token;
        }
    }
}
