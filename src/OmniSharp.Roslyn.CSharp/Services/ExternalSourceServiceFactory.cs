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

        [ImportingConstructor]
        public ExternalSourceServiceFactory(MetadataExternalSourceService metadataExternalSourceService, DecompilationExternalSourceService decompilationExternalSourceService)
        {
            _metadataExternalSourceService = metadataExternalSourceService;
            _decompilationExternalSourceService = decompilationExternalSourceService;
        }

        public IExternalSourceService Create(OmniSharpOptions omniSharpOptions)
            => omniSharpOptions.RoslynExtensionsOptions.EnableDecompilationSupport ? (IExternalSourceService)_decompilationExternalSourceService: _metadataExternalSourceService;

        public CancellationToken CreateCancellationToken(OmniSharpOptions omniSharpOptions, int timeout)
        {
            var enableDecompilationSupport = omniSharpOptions.RoslynExtensionsOptions.EnableDecompilationSupport;
            // since decompilation is slower, use a larger cancellation time (default is 2s per request)
            var cancellationTimeout = enableDecompilationSupport
                ? timeout <= 10000 ? 10000 : timeout // minimum 10s for decompilation
                : timeout; // request defined for metadata

            return new CancellationTokenSource(cancellationTimeout).Token;
        }
    }
}
