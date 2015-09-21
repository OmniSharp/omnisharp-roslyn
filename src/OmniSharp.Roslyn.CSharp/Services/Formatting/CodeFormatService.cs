using System;
using System.Composition;
﻿using System.Threading.Tasks;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Options;
using Microsoft.CodeAnalysis.Text;
using OmniSharp.Mef;
using OmniSharp.Models;
using OmniSharp.Options;
using OmniSharp.Roslyn.CSharp.Workers.Format;

namespace OmniSharp.Roslyn.CSharp.Services.Formatting
{
    [OmniSharpHandler(typeof(RequestHandler<CodeFormatRequest, CodeFormatResponse>), LanguageNames.CSharp)]
    public class CodeFormatService : RequestHandler<CodeFormatRequest, CodeFormatResponse>
    {
        private readonly OmnisharpWorkspace _workspace;
        private readonly OptionSet _options;

        [ImportingConstructor]
        public CodeFormatService(OmnisharpWorkspace workspace, FormattingOptions formattingOptions)
        {
            _workspace = workspace;
            _options = _workspace.Options
                .WithChangedOption(Microsoft.CodeAnalysis.Formatting.FormattingOptions.NewLine, LanguageNames.CSharp, formattingOptions.NewLine)
                .WithChangedOption(Microsoft.CodeAnalysis.Formatting.FormattingOptions.UseTabs, LanguageNames.CSharp, formattingOptions.UseTabs)
                .WithChangedOption(Microsoft.CodeAnalysis.Formatting.FormattingOptions.TabSize, LanguageNames.CSharp, formattingOptions.TabSize);
        }

        public async Task<CodeFormatResponse> Handle(CodeFormatRequest request)
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
    }
}
