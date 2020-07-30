using System.Composition;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.QuickInfo;
using Microsoft.CodeAnalysis.Text;
using Microsoft.Extensions.Logging;
using OmniSharp.Mef;
using OmniSharp.Models;
using OmniSharp.Options;
using OmniSharp.Utilities;

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
        /// Indicates the start of a text container. The elements after <see cref="ContainerStart"/> through (but not
        /// including) the matching <see cref="ContainerEnd"/> are rendered in a rectangular block which is positioned
        /// as an inline element relative to surrounding elements. The text of the <see cref="ContainerStart"/> element
        /// itself precedes the content of the container, and is typically a bullet or number header for an item in a
        /// list.
        /// </summary>
        private const string ContainerStart = nameof(ContainerStart);
        /// <summary>
        /// Indicates the end of a text container. See <see cref="ContainerStart"/>.
        /// </summary>
        private const string ContainerEnd = nameof(ContainerEnd);
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
            var position = sourceText.Lines.GetPosition(new LinePosition(request.Line, request.Column));

            var quickInfo = await quickInfoService.GetQuickInfoAsync(document, position);
            if (quickInfo is null)
            {
                _logger?.LogTrace($"No QuickInfo found for {document.FilePath}:{request.Line},{request.Column}");
                return response;
            }

            var finalTextBuilder = new StringBuilder();
            var sectionTextBuilder = new StringBuilder();

            var description = quickInfo.Sections.FirstOrDefault(s => s.Kind == QuickInfoSectionKinds.Description);
            if (description is object)
            {
                appendSectionAsCsharp(description, finalTextBuilder, _formattingOptions, includeSpaceAtStart: false);
            }

            var summary = quickInfo.Sections.FirstOrDefault(s => s.Kind == QuickInfoSectionKinds.DocumentationComments);
            if (summary is object)
            {
                buildSectionAsMarkdown(summary, sectionTextBuilder, _formattingOptions, out _);
                appendBuiltSection(finalTextBuilder, sectionTextBuilder, _formattingOptions);
            }

            foreach (var section in quickInfo.Sections)
            {
                switch (section.Kind)
                {
                    case QuickInfoSectionKinds.Description:
                    case QuickInfoSectionKinds.DocumentationComments:
                        continue;

                    case QuickInfoSectionKinds.TypeParameters:
                        appendSectionAsCsharp(section, finalTextBuilder, _formattingOptions);
                        break;

                    case QuickInfoSectionKinds.AnonymousTypes:
                        // The first line is "Anonymous Types:"
                        buildSectionAsMarkdown(section, sectionTextBuilder, _formattingOptions, out int lastIndex, untilLineBreak: true);
                        appendBuiltSection(finalTextBuilder, sectionTextBuilder, _formattingOptions);

                        // Then we want all anonymous types to be C# highlighted
                        appendSectionAsCsharp(section, finalTextBuilder, _formattingOptions, lastIndex + 1);
                        break;

                    case NullabilityAnalysis:
                        // Italicize the nullable analysis for emphasis.
                        buildSectionAsMarkdown(section, sectionTextBuilder, _formattingOptions, out _);
                        appendBuiltSection(finalTextBuilder, sectionTextBuilder, _formattingOptions, italicize: true);
                        break;

                    default:
                        buildSectionAsMarkdown(section, sectionTextBuilder, _formattingOptions, out _);
                        appendBuiltSection(finalTextBuilder, sectionTextBuilder, _formattingOptions);
                        break;
                }
            }

            response.Markdown = finalTextBuilder.ToString().Trim();

            return response;

            static void appendBuiltSection(StringBuilder finalTextBuilder, StringBuilder stringBuilder, FormattingOptions formattingOptions, bool italicize = false)
            {
                // Two newlines to trigger a markdown new paragraph
                finalTextBuilder.Append(formattingOptions.NewLine);
                finalTextBuilder.Append(formattingOptions.NewLine);
                if (italicize)
                {
                    finalTextBuilder.Append("_");
                }
                finalTextBuilder.Append(stringBuilder);
                if (italicize)
                {
                    finalTextBuilder.Append("_");
                }
                stringBuilder.Clear();
            }

            static void appendSectionAsCsharp(QuickInfoSection section, StringBuilder builder, FormattingOptions formattingOptions, int startingIndex = 0, bool includeSpaceAtStart = true)
            {
                if (includeSpaceAtStart)
                {
                    builder.Append(formattingOptions.NewLine);
                }
                builder.Append("```csharp");
                builder.Append(formattingOptions.NewLine);
                for (int i = startingIndex; i < section.TaggedParts.Length; i++)
                {
                    TaggedText part = section.TaggedParts[i];
                    if (part.Tag == TextTags.LineBreak && i + 1 != section.TaggedParts.Length)
                    {
                        builder.Append(formattingOptions.NewLine);
                    }
                    else
                    {
                        builder.Append(part.Text);
                    }
                }
                builder.Append(formattingOptions.NewLine);
                builder.Append("```");
            }

            static void buildSectionAsMarkdown(QuickInfoSection section, StringBuilder stringBuilder, FormattingOptions formattingOptions, out int lastIndex, bool untilLineBreak = false)
            {
                bool isInCodeBlock = false;
                lastIndex = 0;
                for (int i = 0; i < section.TaggedParts.Length; i++)
                {
                    var current = section.TaggedParts[i];
                    lastIndex = i;

                    switch (current.Tag)
                    {
                        case TextTags.Text when !isInCodeBlock:
                            addText(current.Text);
                            break;

                        case TextTags.Text:
                            endBlock();
                            addText(current.Text);
                            break;

                        case TextTags.Space when isInCodeBlock:
                            if (nextIsTag(i, TextTags.Text))
                            {
                                endBlock();
                            }

                            stringBuilder.Append(current.Text);
                            break;

                        case TextTags.Punctuation when isInCodeBlock && current.Text != "`":
                            stringBuilder.Append(current.Text);
                            break;

                        case TextTags.Space:
                        case TextTags.Punctuation:
                            stringBuilder.Append(current.Text);
                            break;

                        case ContainerStart:
                            addNewline();
                            addText(current.Text);
                            break;

                        case ContainerEnd:
                            addNewline();
                            break;

                        case TextTags.LineBreak when untilLineBreak && stringBuilder.Length != 0:
                            // The section will end and another newline will be appended, no need to add yet another newline.
                            return;

                        case TextTags.LineBreak:
                            if (stringBuilder.Length != 0 && !nextIsTag(i, ContainerStart, ContainerEnd) && i + 1 != section.TaggedParts.Length)
                            {
                                addNewline();
                            }
                            break;

                        default:
                            if (!isInCodeBlock)
                            {
                                isInCodeBlock = true;
                                stringBuilder.Append('`');
                            }
                            stringBuilder.Append(current.Text);
                            break;
                    }
                }

                if (isInCodeBlock)
                {
                    endBlock();
                }

                return;

                void addText(string text)
                {
                    stringBuilder.Append(MarkdownHelpers.Escape(text));
                }

                void addNewline()
                {
                    if (isInCodeBlock)
                    {
                        endBlock();
                    }

                    // Markdown needs 2 linebreaks to make a new paragraph
                    stringBuilder.Append(formattingOptions.NewLine);
                    stringBuilder.Append(formattingOptions.NewLine);
                }

                void endBlock()
                {
                    stringBuilder.Append('`');
                    isInCodeBlock = false;
                }

                bool nextIsTag(int i, params string[] tags)
                {
                    int nextI = i + 1;
                    return nextI < section.TaggedParts.Length && tags.Contains(section.TaggedParts[nextI].Tag);
                }
            }
        }
    }
}
