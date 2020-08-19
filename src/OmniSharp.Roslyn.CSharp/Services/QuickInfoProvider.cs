using System.Composition;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.QuickInfo;
using Microsoft.Extensions.Logging;
using OmniSharp.Extensions;
using OmniSharp.Mef;
using OmniSharp.Models;
using OmniSharp.Options;
using OmniSharp.Roslyn.CSharp.Helpers;

#nullable enable

namespace OmniSharp.Roslyn.CSharp.Services
{
    [OmniSharpHandler(OmniSharpEndpoints.QuickInfo, LanguageNames.CSharp)]
    public class QuickInfoProvider : IRequestHandler<QuickInfoRequest, QuickInfoResponse>
    {
        // Based on https://github.com/dotnet/roslyn/blob/7dc32a952e77c96c31cae6a2ba6d253a558fc7ff/src/Features/LanguageServer/Protocol/Handler/Hover/HoverHandler.cs

        // These are internal tag values taken from https://github.com/dotnet/roslyn/blob/master/src/Features/Core/Portable/Common/TextTags.cs
        // They're copied here so that we can ensure we render blocks correctly in the markdown
        // https://github.com/dotnet/roslyn/issues/46254 tracks making these public

        /// <summary>
        /// Section kind for nullability analysis.
        /// </summary>
        internal const string NullabilityAnalysis = nameof(NullabilityAnalysis);

        private readonly OmniSharpWorkspace _workspace;
        private readonly FormattingOptions _formattingOptions;
        private readonly ILogger<QuickInfoProvider>? _logger;

        [ImportingConstructor]
        public QuickInfoProvider(OmniSharpWorkspace workspace, FormattingOptions formattingOptions, ILoggerFactory? loggerFactory)
        {
            _workspace = workspace;
            _formattingOptions = formattingOptions;
            _logger = loggerFactory?.CreateLogger<QuickInfoProvider>();
        }

        public async Task<QuickInfoResponse> Handle(QuickInfoRequest request)
        {
            var document = _workspace.GetDocument(request.FileName);
            var response = new QuickInfoResponse();

            if (document is null)
            {
                return response;
            }

            var quickInfoService = QuickInfoService.GetService(document);
            if (quickInfoService is null)
            {
                _logger?.LogWarning($"QuickInfo service was null for {document.FilePath}");
                return response;
            }

            var sourceText = await document.GetTextAsync();
            var position = sourceText.GetTextPosition(request);

            var quickInfo = await quickInfoService.GetQuickInfoAsync(document, position);
            if (quickInfo is null)
            {
                _logger?.LogTrace($"No QuickInfo found for {document.FilePath}:{request.Line},{request.Column}");
                return response;
            }

            var finalTextBuilder = new StringBuilder();

            bool lastSectionHadLineBreak = true;
            var description = quickInfo.Sections.FirstOrDefault(s => s.Kind == QuickInfoSectionKinds.Description);
            if (description is object)
            {
                appendSection(description, MarkdownFormat.AllTextAsCSharp);
            }

            var summary = quickInfo.Sections.FirstOrDefault(s => s.Kind == QuickInfoSectionKinds.DocumentationComments);
            if (summary is object)
            {
                appendSection(summary, MarkdownFormat.Default);
            }

            foreach (var section in quickInfo.Sections)
            {
                switch (section.Kind)
                {
                    case QuickInfoSectionKinds.Description:
                    case QuickInfoSectionKinds.DocumentationComments:
                        continue;

                    case QuickInfoSectionKinds.TypeParameters:
                        appendSection(section, MarkdownFormat.AllTextAsCSharp);
                        break;

                    case QuickInfoSectionKinds.AnonymousTypes:
                        // The first line is "Anonymous Types:"
                        // Then we want all anonymous types to be C# highlighted
                        appendSection(section, MarkdownFormat.FirstLineDefaultRestCSharp);
                        break;

                    case NullabilityAnalysis:
                        // Italicize the nullable analysis for emphasis.
                        appendSection(section, MarkdownFormat.Italicize);
                        break;

                    default:
                        appendSection(section, MarkdownFormat.Default);
                        break;
                }
            }

            response.Markdown = finalTextBuilder.ToString().Trim();

            return response;

            void appendSection(QuickInfoSection section, MarkdownFormat format)
            {
                if (!lastSectionHadLineBreak && !section.TaggedParts.StartsWithNewline())
                {
                    finalTextBuilder.Append(_formattingOptions.NewLine);
                    finalTextBuilder.Append(_formattingOptions.NewLine);
                }
                MarkdownHelpers.TaggedTextToMarkdown(section.TaggedParts, finalTextBuilder, _formattingOptions, format, out lastSectionHadLineBreak);
            }
        }
    }
}
