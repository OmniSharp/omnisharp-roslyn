using System;
using System.Composition;
﻿using System.Threading.Tasks;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Options;
using Microsoft.CodeAnalysis.Text;
using OmniSharp.Roslyn.CSharp.Workers.Format;
using OmniSharp.Models;
using OmniSharp.Options;

namespace OmniSharp.Roslyn.CSharp.Services.Formatting
{
    [Export(typeof(RequestHandler<FormatAfterKeystrokeRequest, FormatRangeResponse>))]
    [Export(typeof(RequestHandler<FormatRangeRequest, FormatRangeResponse>))]
    [Export(typeof(RequestHandler<Request, CodeFormatResponse>))]
    public class CodeFormattingService :
        RequestHandler<FormatAfterKeystrokeRequest, FormatRangeResponse>,
        RequestHandler<FormatRangeRequest, FormatRangeResponse>,
        RequestHandler<Request, CodeFormatResponse>
    {
        private readonly OmnisharpWorkspace _workspace;
        private readonly OptionSet _options;

        [ImportingConstructor]
        public CodeFormattingService(OmnisharpWorkspace workspace, FormattingOptions formattingOptions)
        {
            _workspace = workspace;
            _options = _workspace.Options
                .WithChangedOption(Microsoft.CodeAnalysis.Formatting.FormattingOptions.NewLine, LanguageNames.CSharp, formattingOptions.NewLine)
                .WithChangedOption(Microsoft.CodeAnalysis.Formatting.FormattingOptions.UseTabs, LanguageNames.CSharp, formattingOptions.UseTabs)
                .WithChangedOption(Microsoft.CodeAnalysis.Formatting.FormattingOptions.TabSize, LanguageNames.CSharp, formattingOptions.TabSize);
        }

        async Task<FormatRangeResponse> RequestHandler<FormatAfterKeystrokeRequest, FormatRangeResponse>.Handle(FormatAfterKeystrokeRequest request)
        {
            var document = _workspace.GetDocument(request.FileName);
            if (document == null)
            {
                return null;
            }

            var lines = (await document.GetSyntaxTreeAsync()).GetText().Lines;
            int position = lines.GetPosition(new LinePosition(request.Line - 1, request.Column - 1));
            var changes = await OmniSharp.Roslyn.CSharp.Workers.Format.Formatting.GetFormattingChangesAfterKeystroke(_workspace, _options, document, position, request.Char);

            return new FormatRangeResponse()
            {
                Changes = changes
            };
        }

        async Task<CodeFormatResponse> RequestHandler<Request, CodeFormatResponse>.Handle(Request request)
        {
            var document = _workspace.GetDocument(request.FileName);
            if (document == null)
            {
                return null;
            }

            var newText = await OmniSharp.Roslyn.CSharp.Workers.Format.Formatting.GetFormattedDocument(_workspace, _options, document);
            return new CodeFormatResponse()
            {
                Buffer = newText
            };
        }

        async Task<FormatRangeResponse> RequestHandler<FormatRangeRequest, FormatRangeResponse>.Handle(FormatRangeRequest request)
        {
            var document = _workspace.GetDocument(request.FileName);
            if (document == null)
            {
                return null;
            }

            var lines = (await document.GetSyntaxTreeAsync()).GetText().Lines;
            var start = lines.GetPosition(new LinePosition(request.Line - 1, request.Column - 1));
            var end = lines.GetPosition(new LinePosition(request.EndLine - 1, request.EndColumn - 1));
            var changes = await OmniSharp.Roslyn.CSharp.Workers.Format.Formatting.GetFormattingChangesForRange(_workspace, _options, document, start, end);

            return new FormatRangeResponse()
            {
                Changes = changes
            };
        }
    }
}
