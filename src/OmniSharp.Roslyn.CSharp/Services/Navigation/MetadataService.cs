using System;
using System.Composition;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.CodeAnalysis;
using Newtonsoft.Json;
using OmniSharp.Mef;
using OmniSharp.Models.Metadata;
using OmniSharp.Options;
using OmniSharp.Roslyn.CSharp.Services.Decompilation;

namespace OmniSharp.Roslyn.CSharp.Services.Navigation
{
    [OmniSharpHandler(OmniSharpEndpoints.Metadata, LanguageNames.CSharp)]
    public class MetadataService : IRequestHandler<MetadataRequest, MetadataResponse>
    {
        private readonly MetadataHelper _metadataHelper;
        private readonly DecompilationHelper _decompilationHelper;
        private readonly OmniSharpOptions _omniSharpOptions;
        private readonly OmniSharpWorkspace _workspace;

        [ImportingConstructor]
        public MetadataService(OmniSharpWorkspace workspace, MetadataHelper metadataHelper, DecompilationHelper decompilationHelper, OmniSharpOptions omniSharpOptions)
        {
            _workspace = workspace;
            _metadataHelper = metadataHelper;
            _decompilationHelper = decompilationHelper;
            _omniSharpOptions = omniSharpOptions;
        }

        public async Task<MetadataResponse> Handle(MetadataRequest request)
        {
            var response = new MetadataResponse();
            foreach (var project in _workspace.CurrentSolution.Projects)
            {
                var compilation = await project.GetCompilationAsync();
                var symbol = compilation.GetTypeByMetadataName(request.TypeName);
                if (symbol != null && symbol.ContainingAssembly.Name == request.AssemblyName)
                {
                    var cancellationSource = new CancellationTokenSource(TimeSpan.FromMilliseconds(request.Timeout));

                    // we only support decompilation when running on net472
                    // due to dependency on Microsoft.CodeAnalysis.Editor.CSharp
#if NET472
                    var enableDecompilationSupport = _omniSharpOptions.RoslynExtensionsOptions.EnableDecompilationSupport;
#else
                    var enableDecompilationSupport = false;
#endif

                    var (metadataDocument, documentPath) = enableDecompilationSupport ?
                        await _decompilationHelper.GetAndAddDecompiledDocument(project, symbol, cancellationSource.Token) :
                        await _metadataHelper.GetAndAddDocumentFromMetadata(project, symbol, cancellationSource.Token);

                    if (metadataDocument != null)
                    {
                        var source = await metadataDocument.GetTextAsync();
                        response.Source = source.ToString();
                        response.SourceName = documentPath;

                        return response;
                    }
                }
            }
            return response;
        }
    }
}
