using System.Collections.Immutable;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using Microsoft.CodeAnalysis;
using OmniSharp.Options;

namespace OmniSharp.Roslyn.CSharp.Helpers
{
    public static class MarkdownHelpers
    {
        private static Regex EscapeRegex = new Regex(@"([\\`\*_\{\}\[\]\(\)#+\-\.!])", RegexOptions.Compiled);

        public static string Escape(string markdown)
        {
            if (markdown == null)
                return null;
            return EscapeRegex.Replace(markdown, @"\$1");
        }

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

        public static void TaggedTextToMarkdown(ImmutableArray<TaggedText> taggedParts, StringBuilder stringBuilder, FormattingOptions formattingOptions, out int lastIndex, bool untilLineBreak = false)
        {
            bool isInCodeBlock = false;
            bool brokeLine = true;
            lastIndex = 0;
            for (int i = 0; i < taggedParts.Length; i++)
            {
                var current = taggedParts[i];
                lastIndex = i;

                if (brokeLine)
                {
                    Debug.Assert(!isInCodeBlock);
                    // If we're on a new line and there are no text parts in the upcoming line, then we
                    // can format the whole line as C# code instead of plaintext. Otherwise, we need to
                    // intermix, and can only use simple ` codefences
                    brokeLine = false;
                    bool canFormatAsBlock = false;
                    for (int j = i; j < taggedParts.Length; j++)
                    {
                        switch (taggedParts[j].Tag)
                        {
                            case TextTags.Text:
                                canFormatAsBlock = false;
                                goto endOfLineOrTextFound;

                            case ContainerStart:
                            case ContainerEnd:
                            case TextTags.LineBreak:
                                goto endOfLineOrTextFound;

                            default:
                                // If the block is just newlines, then we don't want to format that as
                                // C# code. So, we default to false, set it to true if there's actually
                                // content on the line, then set to false again if Text content is
                                // encountered.
                                canFormatAsBlock = true;
                                continue;
                        }
                    }

                endOfLineOrTextFound:
                    if (canFormatAsBlock)
                    {
                        stringBuilder.Append("```csharp");
                        stringBuilder.Append(formattingOptions.NewLine);
                        for (; i < taggedParts.Length; i++)
                        {
                            current = taggedParts[i];
                            if (current.Tag == ContainerStart
                                || current.Tag == ContainerEnd
                                || current.Tag == TextTags.LineBreak)
                            {
                                // End the codeblock
                                stringBuilder.Append(formattingOptions.NewLine);
                                stringBuilder.Append("```");

                                // We know we're at a line break of some kind, but it could be
                                // a container start, so let the standard handling take care of it.
                                goto standardHandling;
                            }

                            stringBuilder.Append(current.Text);
                        }

                        // If we're here, that means that the last part has been reached, so just
                        // return.
                        Debug.Assert(i == taggedParts.Length);
                        stringBuilder.Append(formattingOptions.NewLine);
                        stringBuilder.Append("```");
                        return;
                    }
                }

            standardHandling:
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
                        if (indexIsTag(i + 1, TextTags.Text))
                        {
                            endBlock();
                        }

                        addText(current.Text);
                        break;

                    case TextTags.Space:
                    case TextTags.Punctuation:
                        addText(current.Text);
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
                        if (stringBuilder.Length != 0 && !indexIsTag(i + 1, ContainerStart, ContainerEnd) && i + 1 != taggedParts.Length)
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
                stringBuilder.Append(Escape(text));
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
                brokeLine = true;
            }

            void endBlock()
            {
                stringBuilder.Append('`');
                isInCodeBlock = false;
            }

            bool indexIsTag(int i, params string[] tags)
                => i < taggedParts.Length && tags.Contains(taggedParts[i].Tag);
        }
    }
}
