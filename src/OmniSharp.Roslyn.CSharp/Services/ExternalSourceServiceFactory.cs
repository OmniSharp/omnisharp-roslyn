using OmniSharp.Options;
using OmniSharp.Roslyn.CSharp.Services.Decompilation;
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
        private readonly MetadataExternalSourceService _metadataExternalSourceService;
        private readonly DecompilationExternalSourceService _decompilationExternalSourceService;

        [ImportingConstructor]
        public ExternalSourceServiceFactory(MetadataExternalSourceService metadataExternalSourceService, DecompilationExternalSourceService decompilationExternalSourceService)
        {
            _metadataExternalSourceService = metadataExternalSourceService;
            _decompilationExternalSourceService = decompilationExternalSourceService;
        }

        public IExternalSourceService Create(OmniSharpOptions omniSharpOptions)
        {
            // we only support decompilation when running on net472
            // due to dependency on Microsoft.CodeAnalysis.Editor.CSharp
#if NET472
            var enableDecompilationSupport = omniSharpOptions.RoslynExtensionsOptions.EnableDecompilationSupport;
#else
            var enableDecompilationSupport = false;
#endif

            if (enableDecompilationSupport)
            {
                return _decompilationExternalSourceService;
            }

            return _metadataExternalSourceService;
        }

        public CancellationToken CreateCancellationToken(OmniSharpOptions omniSharpOptions, int timeout)
        {
#if NET472
            var enableDecompilationSupport = omniSharpOptions.RoslynExtensionsOptions.EnableDecompilationSupport;
#else
            var enableDecompilationSupport = false;
#endif

            // since decompilation is slower, use a larger cancellation time (default is 2s per request)
            var cancellationTimeout = enableDecompilationSupport
                ? timeout <= 10000 ? 10000 : timeout // minimum 10s for decompilation
                : timeout; // request defined for metadata

            return new CancellationTokenSource(timeout).Token;
        }
    }
}
