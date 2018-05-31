using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Text;
using OmniSharp.Mef;
using OmniSharp.Models;
using OmniSharp.Services;
using OmniSharp.Utilities;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Composition;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;

namespace OmniSharp.Roslyn.CSharp.Services.Structure
{
    [OmniSharpHandler(OmniSharpEndpoints.BlockStructure, LanguageNames.CSharp)]
    public class BlockStructureService : IRequestHandler<Request, IEnumerable<QuickFix>>
    {
        private readonly IAssemblyLoader _loader;
        private readonly Lazy<Assembly> _featureAssembly;
        private readonly Lazy<Type> _blockStructureService;
        private readonly Lazy<Type> _blockStructure;
        private readonly Lazy<Type> _blockSpan;
        private readonly Lazy<MethodInfo> _getBlockStructure;
        private readonly PropertyInfo _getSpans;
        private readonly PropertyInfo _getIsCollpasible;
        private readonly PropertyInfo _getTextSpan;
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
            _getSpans = _blockStructure.Value.GetProperty("Spans");
            _getIsCollpasible = _blockSpan.Value.GetProperty("IsCollapsible");
            _getTextSpan = _blockSpan.Value.GetProperty("TextSpan");
        }

        public async Task<IEnumerable<QuickFix>> Handle(Request request)
        {
            var document = _workspace.GetDocument(request.FileName);
            var text = await document.GetTextAsync();
            var lines = text.Lines;

            var service = _blockStructureService.LazyGetMethod("GetService").InvokeStatic(new[] { document });

            var structure = _getBlockStructure.Invoke<object>(service, new object[] { document, CancellationToken.None });
            IEnumerable spans = _getSpans.GetMethod.Invoke<IEnumerable>(structure, Array.Empty<object>());

            List<QuickFix> outliningSpans = new List<QuickFix>();
            foreach (var span in spans)
            {
                if (_getIsCollpasible.GetMethod.Invoke<bool>(span, Array.Empty<object>()))
                {
                    var textSpan = _getTextSpan.GetMethod.Invoke<TextSpan>(span, Array.Empty<object>());
                    outliningSpans.Add(new QuickFix()
                    {
                        Line = lines.GetLineFromPosition(textSpan.Start).LineNumber,
                        EndLine = lines.GetLineFromPosition(textSpan.End).LineNumber
                    });
                }
            }

            return outliningSpans;
        }
    }
}
