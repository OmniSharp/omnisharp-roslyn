using System;
using System.Collections;
using System.Collections.Generic;
using System.Composition;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Text;
using OmniSharp.Mef;
using OmniSharp.Models.MembersFlat;
using OmniSharp.Models.v2;
using OmniSharp.Models.V2;
using OmniSharp.Roslyn.Extensions;
using OmniSharp.Services;
using OmniSharp.Utilities;

namespace OmniSharp.Roslyn.CSharp.Services.Structure
{
    [OmniSharpHandler(OmniSharpEndpoints.V2.BlockStructure, LanguageNames.CSharp)]
    public class BlockStructureService : IRequestHandler<BlockStructureRequest, BlockStructureResponse>
    {
        private readonly IAssemblyLoader _loader;
        private readonly Lazy<Assembly> _featureAssembly;
        private readonly Lazy<Type> _blockStructureService;
        private readonly Lazy<Type> _blockStructure;
        private readonly Lazy<Type> _blockSpan;
        private readonly Lazy<MethodInfo> _getBlockStructure;
        private readonly MethodInfo _getSpans;
        private readonly MethodInfo _getIsCollpasible;
        private readonly MethodInfo _getTextSpan;
        private readonly MethodInfo _getHintSpan;
        private readonly MethodInfo _getBannerText;
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
            _getHintSpan = _blockSpan.Value.GetProperty("HintSpan").GetMethod;
            _getBannerText = _blockSpan.Value.GetProperty("BannerText").GetMethod;
            _getType = _blockSpan.Value.GetProperty("Type").GetMethod;
        }

        public async Task<BlockStructureResponse> Handle(BlockStructureRequest request)
        {
            var document = _workspace.GetDocument(request.FileName);
            var text = await document.GetTextAsync();
            var lines = text.Lines;

            var service = _blockStructureService.LazyGetMethod("GetService").InvokeStatic(new[] { document });

            var structure = _getBlockStructure.Invoke<object>(service, new object[] { document, CancellationToken.None });
            var spans = _getSpans.Invoke<IEnumerable>(structure, Array.Empty<object>());


            var outliningSpans = new List<BlockSpan>();
            foreach (var span in spans)
            {
                if (_getIsCollpasible.Invoke<bool>(span, Array.Empty<object>()))
                {
                    var textSpan = _getTextSpan.Invoke<TextSpan>(span, Array.Empty<object>());
                    var line = lines.GetLineFromPosition(textSpan.Start);
                    var column = textSpan.Start - line.Start;
                    var endLine = lines.GetLineFromPosition(textSpan.End);
                    var endColumn = textSpan.End - endLine.Start;

                    outliningSpans.Add(new BlockSpan(
                        textSpan.ToRange(lines),
                        hintSpan: _getHintSpan.Invoke<TextSpan>(span, Array.Empty<object>()).ToRange(lines),
                        bannerText: _getBannerText.Invoke<string>(span, Array.Empty<object>()),
                        type: _getType.Invoke<string>(span, Array.Empty<object>())));
                }
            }

            return new BlockStructureResponse() { Spans = outliningSpans };
        }

        
    }
}
