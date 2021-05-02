using System;
using System.Collections;
using System.Collections.Generic;
using System.Composition;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Text;
using OmniSharp.Extensions;
using OmniSharp.Mef;
using OmniSharp.Models.V2;
using OmniSharp.Services;
using OmniSharp.Utilities;

namespace OmniSharp.Roslyn.CSharp.Services.Structure
{
    [OmniSharpHandler(OmniSharpEndpoints.V2.BlockStructure, LanguageNames.CSharp)]
    public class BlockStructureService : IRequestHandler<BlockStructureRequest, BlockStructureResponse>
    {
        private const string PreprocessorRegion = nameof(PreprocessorRegion);

        private readonly IAssemblyLoader _loader;
        private readonly Lazy<Assembly> _featureAssembly;
        private readonly Lazy<Type> _blockStructureService;
        private readonly Lazy<Type> _blockStructure;
        private readonly Lazy<Type> _blockSpan;
        private readonly Lazy<MethodInfo> _getBlockStructure;
        private readonly MethodInfo _getSpans;
        private readonly MethodInfo _getIsCollpasible;
        private readonly MethodInfo _getTextSpan;
        private readonly MethodInfo _getType;
        private readonly OmniSharpWorkspace _workspace;

        [ImportingConstructor]
        public BlockStructureService(IAssemblyLoader loader, OmniSharpWorkspace workspace)
        {
            _workspace = workspace;
            _loader = loader;
            _featureAssembly = _loader.LazyLoad(Configuration.RoslynFeatures);

            _blockStructureService = _featureAssembly.LazyGetType("Microsoft.CodeAnalysis.Structure.BlockStructureService");
            _blockStructure = _featureAssembly.LazyGetType("Microsoft.CodeAnalysis.Structure.BlockStructure");
            _blockSpan = _featureAssembly.LazyGetType("Microsoft.CodeAnalysis.Structure.BlockSpan");

            _getBlockStructure = _blockStructureService.LazyGetMethod("GetBlockStructure");
            _getSpans = _blockStructure.Value.GetProperty("Spans").GetMethod;
            _getIsCollpasible = _blockSpan.Value.GetProperty("IsCollapsible").GetMethod;
            _getTextSpan = _blockSpan.Value.GetProperty("TextSpan").GetMethod;
            _getType = _blockSpan.Value.GetProperty("Type").GetMethod;
        }

        public async Task<BlockStructureResponse> Handle(BlockStructureRequest request)
        {
            // To provide complete code structure for the document wait until all projects are loaded.
            var document = await _workspace.GetDocumentFromFullProjectModelAsync(request.FileName);
            if (document == null)
            {
                return null;
            }

            var text = await document.GetTextAsync();

            var service = _blockStructureService.LazyGetMethod("GetService").InvokeStatic(new[] { document });

            var structure = _getBlockStructure.Invoke<object>(service, new object[] { document, CancellationToken.None });
            var spans = _getSpans.Invoke<IEnumerable>(structure, Array.Empty<object>());


            var outliningSpans = new List<CodeFoldingBlock>();
            foreach (var span in spans)
            {
                if (_getIsCollpasible.Invoke<bool>(span, Array.Empty<object>()))
                {
                    var textSpan = _getTextSpan.Invoke<TextSpan>(span, Array.Empty<object>());

                    outliningSpans.Add(new CodeFoldingBlock(
                        text.GetRangeFromSpan(textSpan),
                        type: ConvertToWellKnownBlockType(_getType.Invoke<string>(span, Array.Empty<object>()))));
                }
            }

            return new BlockStructureResponse() { Spans = outliningSpans };
        }

        private string ConvertToWellKnownBlockType(string kind)
        {
            return kind == CodeFoldingBlockKinds.Comment || kind == CodeFoldingBlockKinds.Imports
                ? kind
                : kind == PreprocessorRegion
                    ? CodeFoldingBlockKinds.Region
                    : null;
        }
    }
}
