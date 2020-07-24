using System.Collections.Immutable;
using System.Composition;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.QuickInfo;
using Microsoft.CodeAnalysis.Text;
using OmniSharp.Mef;
using OmniSharp.Models.v2;
using OmniSharp.Options;

#nullable enable

namespace OmniSharp.Roslyn.CSharp.Services
{
    [OmniSharpHandler(OmniSharpEndpoints.V2.QuickInfo, LanguageNames.CSharp)]
    public class QuickInfoProvider : IRequestHandler<QuickInfoRequest, QuickInfoResponse>
    {
        // Based on https://github.com/dotnet/roslyn/blob/master/src/Features/LanguageServer/Protocol/Handler/Hover/HoverHandler.cs

        // These are internal tag values taken from https://github.com/dotnet/roslyn/blob/master/src/Features/Core/Portable/Common/TextTags.cs
        // They're copied here so that we can ensure we render blocks correctly in the markdown

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

        private readonly OmniSharpWorkspace _workspace;
        private readonly FormattingOptions _formattingOptions;

        [ImportingConstructor]
        public QuickInfoProvider(OmniSharpWorkspace workspace, FormattingOptions formattingOptions)
        {
            _workspace = workspace;
            _formattingOptions = formattingOptions;
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
                return response;
            }

            var sourceText = await document.GetTextAsync();
            var position = sourceText.Lines.GetPosition(new LinePosition(request.Line, request.Column));

            var quickInfo = await quickInfoService.GetQuickInfoAsync(document, position);
            if (quickInfo is null)
            {
                return response;
            }


            var sb = new StringBuilder();
            response.Description = quickInfo.Sections.FirstOrDefault(s => s.Kind == QuickInfoSectionKinds.Description)?.Text;

            var documentation = quickInfo.Sections.FirstOrDefault(s => s.Kind == QuickInfoSectionKinds.DocumentationComments);
            if (documentation is object)
            {
                response.Summary = getMarkdown(documentation.TaggedParts);
            }

            response.RemainingSections = quickInfo.Sections
                                                  .Where(s => s.Kind != QuickInfoSectionKinds.Description && s.Kind != QuickInfoSectionKinds.DocumentationComments)
                                                  .Select(s =>
                                                  {
                                                      switch (s.Kind)
                                                      {
                                                          case QuickInfoSectionKinds.AnonymousTypes:
                                                          case QuickInfoSectionKinds.TypeParameters:
                                                              return new QuickInfoResponseSection { IsCSharpCode = true, Text = s.Text };

                                                          default:
                                                              return new QuickInfoResponseSection { IsCSharpCode = false, Text = getMarkdown(s.TaggedParts) };
                                                      }
                                                  })
                                                  .ToArray();

            return response;

            string getMarkdown(ImmutableArray<TaggedText> taggedTexts)
            {
                bool isInCodeBlock = false;
                var sb = new StringBuilder();
                for (int i = 0; i < taggedTexts.Length; i++)
                {
                    var current = taggedTexts[i];

                    switch (current.Tag)
                    {
                        case TextTags.Text when !isInCodeBlock:
                            sb.Append(current.Text);
                            break;

                        case TextTags.Text:
                            endBlock();
                            sb.Append(current.Text);
                            break;

                        case TextTags.Space when isInCodeBlock:
                            if (nextIsTag(TextTags.Text, i))
                            {
                                endBlock();
                            }

                            sb.Append(current.Text);
                            break;

                        case TextTags.Space:
                        case TextTags.Punctuation:
                            sb.Append(current.Text);
                            break;

                        case ContainerStart:
                            // Markdown needs 2 linebreaks to make a new paragraph
                            addNewline();
                            addNewline();
                            sb.Append(current.Text);
                            break;

                        case ContainerEnd:
                            // Markdown needs 2 linebreaks to make a new paragraph
                            addNewline();
                            addNewline();
                            break;

                        case TextTags.LineBreak:
                            if (!nextIsTag(ContainerStart, i) && !nextIsTag(ContainerEnd, i))
                            {
                                addNewline();
                                addNewline();
                            }
                            break;

                        default:
                            if (!isInCodeBlock)
                            {
                                isInCodeBlock = true;
                                sb.Append('`');
                            }
                            sb.Append(current.Text);
                            break;
                    }
                }

                if (isInCodeBlock)
                {
                    endBlock();
                }

                return sb.ToString().Trim();

                void addNewline()
                {
                    if (isInCodeBlock)
                    {
                        endBlock();
                    }

                    sb.Append(_formattingOptions.NewLine);
                }

                void endBlock()
                {
                    sb.Append('`');
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
