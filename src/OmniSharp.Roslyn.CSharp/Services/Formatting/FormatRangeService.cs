using System.Composition;
using System.Threading.Tasks;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Text;
using Microsoft.Extensions.Logging;
using OmniSharp.Mef;
using OmniSharp.Models.Format;
using OmniSharp.Options;
using OmniSharp.Roslyn.CSharp.Workers.Formatting;

namespace OmniSharp.Roslyn.CSharp.Services.Formatting
{
    [OmniSharpHandler(OmniSharpEndpoints.FormatRange, LanguageNames.CSharp)]
    public class FormatRangeService : IRequestHandler<FormatRangeRequest, FormatRangeResponse>
    {
        private readonly OmniSharpWorkspace _workspace;
        private readonly OmniSharpOptions _omnisharpOptions;
        private readonly ILoggerFactory _loggerFactory;

        [ImportingConstructor]
        public FormatRangeService(OmniSharpWorkspace workspace, OmniSharpOptions omnisharpOptions, ILoggerFactory loggerFactory)
        {
            _workspace = workspace;
            _omnisharpOptions = omnisharpOptions;
            _loggerFactory = loggerFactory;
        }

        public async Task<FormatRangeResponse> Handle(FormatRangeRequest request)
        {
            var document = _workspace.GetDocument(request.FileName);
            if (document == null)
            {
                return null;
            }

            var text = await document.GetTextAsync();
            var start = text.Lines.GetPosition(new LinePosition(request.Line, request.Column));
            var end = text.Lines.GetPosition(new LinePosition(request.EndLine, request.EndColumn));
            var syntaxTree = await document.GetSyntaxRootAsync();
            var tokenStart = syntaxTree.FindToken(start).FullSpan.Start;
            var changes = await FormattingWorker.GetFormattingChanges(document, tokenStart, end, _omnisharpOptions, _loggerFactory);

            return new FormatRangeResponse()
            {
                Changes = changes
            };
        }
    }
}
