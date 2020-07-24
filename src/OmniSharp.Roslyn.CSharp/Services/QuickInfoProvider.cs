using System.Collections.Immutable;
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
            var response = new QuickInfoResponse() { Sections = ImmutableArray<QuickInfoResponseSection>.Empty };

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

            var sectionBuilder = ImmutableArray.CreateBuilder<QuickInfoResponseSection>(quickInfo.Sections.Length);
            var stringBuilder = new StringBuilder();

            bool foundDescription = false;
            foreach (var section in quickInfo.Sections)
            {
                switch (section.Kind)
                {
                    case QuickInfoSectionKinds.Description:
                        sectionBuilder.Insert(0, new QuickInfoResponseSection { IsCSharpCode = true, Text = section.Text });
                        foundDescription = true;
                        break;

                    case QuickInfoSectionKinds.TypeParameters:
                        stringBuilder.Clear();
                        foreach (var text in section.TaggedParts)
                        {
                            switch (text.Tag)
                            {
                                case TextTags.LineBreak:
                                    appendIfNeeded();
                                    stringBuilder.Clear();
                                    continue;

                                default:
                                    stringBuilder.Append(text.Text);
                                    break;
                            }
                        }

                        appendIfNeeded();
                        break;

                        void appendIfNeeded()
                        {
                            var currentString = stringBuilder.ToString().Trim();
                            if (currentString == string.Empty)
                            {
                                return;
                            }

                            sectionBuilder.Add(new QuickInfoResponseSection { IsCSharpCode = true, Text = currentString });
                        }

                    case QuickInfoSectionKinds.AnonymousTypes:
                        sectionBuilder.Add(new QuickInfoResponseSection { IsCSharpCode = false, Text = getMarkdown(section.TaggedParts, stringBuilder, _formattingOptions, out int remainingIndex, untilLineBreak: true) });

                        if (remainingIndex < section.TaggedParts.Length)
                        {
                            sectionBuilder.Add(new QuickInfoResponseSection
                            {
                                IsCSharpCode = true,
                                Text = string.Concat(section.TaggedParts.Skip(remainingIndex + 1).Select(s => s.Text))
                            });
                        }

                        break;

                    case QuickInfoSectionKinds.DocumentationComments:
                        sectionBuilder.Insert(foundDescription ? 1 : 0,
                            new QuickInfoResponseSection { IsCSharpCode = false, Text = getMarkdown(section.TaggedParts, stringBuilder, _formattingOptions, out _) });
                        break;

                    case NullabilityAnalysis:
                        var nullabilityText = getMarkdown(section.TaggedParts, stringBuilder, _formattingOptions, out _);
                        if (!nullabilityText.Contains(_formattingOptions.NewLine))
                        {
                            // Italicize the text for emphasis
                            nullabilityText = $"_{nullabilityText}_";
                        }
                        sectionBuilder.Add(new QuickInfoResponseSection { IsCSharpCode = false, Text = nullabilityText });
                        break;

                    default:
                        sectionBuilder.Add(new QuickInfoResponseSection { IsCSharpCode = false, Text = getMarkdown(section.TaggedParts, stringBuilder, _formattingOptions, out _) });
                        break;
                }
            }

            response.Sections = sectionBuilder.ToImmutable();

            return response;

            static string getMarkdown(ImmutableArray<TaggedText> taggedTexts, StringBuilder stringBuilder, FormattingOptions formattingOptions, out int lastIndex, bool untilLineBreak = false)
            {
                bool isInCodeBlock = false;
                stringBuilder.Clear();
                lastIndex = 0;
                for (int i = 0; i < taggedTexts.Length; i++)
                {
                    var current = taggedTexts[i];
                    lastIndex = i;

                    switch (current.Tag)
                    {
                        case TextTags.Text when !isInCodeBlock:
                            stringBuilder.Append(current.Text);
                            break;

                        case TextTags.Text:
                            endBlock();
                            stringBuilder.Append(current.Text);
                            break;

                        case TextTags.Space when isInCodeBlock:
                            if (nextIsTag(TextTags.Text, i))
                            {
                                endBlock();
                            }

                            stringBuilder.Append(current.Text);
                            break;

                        case TextTags.Space:
                        case TextTags.Punctuation:
                            stringBuilder.Append(current.Text);
                            break;

                        case ContainerStart:
                            addNewline();
                            stringBuilder.Append(current.Text);
                            break;

                        case ContainerEnd:
                            addNewline();
                            break;

                        case TextTags.LineBreak when untilLineBreak
                                                     && stringBuilder.ToString().Trim() is var currentString
                                                     && currentString != string.Empty:
                            addNewline();
                            return currentString;

                        case TextTags.LineBreak:
                            if (!nextIsTag(ContainerStart, i) && !nextIsTag(ContainerEnd, i))
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

                return stringBuilder.ToString().Trim();

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

                bool nextIsTag(string tag, int i)
                {
                    int nextI = i + 1;
                    return nextI < taggedTexts.Length && taggedTexts[nextI].Tag == tag;
                }
            }
        }
    }
}
