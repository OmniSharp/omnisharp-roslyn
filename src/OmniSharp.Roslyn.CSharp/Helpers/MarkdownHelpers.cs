using System.Collections.Immutable;
using System.Diagnostics;
using System.Linq;
using System.Runtime.InteropServices;
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

        public static bool StartsWithNewline(this ImmutableArray<TaggedText> taggedParts)
        {
            return !taggedParts.IsDefaultOrEmpty
                   && taggedParts[0].Tag switch { TextTags.LineBreak => true, ContainerStart => true, _ => false };
        }

        public static void TaggedTextToMarkdown(
            ImmutableArray<TaggedText> taggedParts,
            StringBuilder stringBuilder,
            FormattingOptions formattingOptions,
            MarkdownFormat markdownFormat,
            out bool endedWithLineBreak)
        {
            bool isInCodeBlock = false;
            bool brokeLine = true;
            bool afterFirstLine = false;

            if (markdownFormat == MarkdownFormat.Italicize)
            {
                stringBuilder.Append("_");
            }

            for (int i = 0; i < taggedParts.Length; i++)
            {
                var current = taggedParts[i];

                if (brokeLine && markdownFormat != MarkdownFormat.Italicize)
                {
                    Debug.Assert(!isInCodeBlock);
                    brokeLine = false;
                    bool canFormatAsBlock = (afterFirstLine, markdownFormat) switch
                    {
                        (false, MarkdownFormat.FirstLineAsCSharp) => true,
                        (true, MarkdownFormat.FirstLineDefaultRestCSharp) => true,
                        (_, MarkdownFormat.AllTextAsCSharp) => true,
                        _ => false
                    };

                    if (!canFormatAsBlock)
                    {
                        // If we're on a new line and there are no text parts in the upcoming line, then we
                        // can format the whole line as C# code instead of plaintext. Otherwise, we need to
                        // intermix, and can only use simple ` codefences
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
                    }
                    else
                    {
                        // If it's just a newline, we're going to default to standard handling which will
                        // skip the newline.
                        canFormatAsBlock = !indexIsTag(i, ContainerStart, ContainerEnd, TextTags.LineBreak);
                    }

                endOfLineOrTextFound:
                    if (canFormatAsBlock)
                    {
                        afterFirstLine = true;
                        stringBuilder.Append("```csharp");
                        stringBuilder.Append(formattingOptions.NewLine);
                        for (; i < taggedParts.Length; i++)
                        {
                            current = taggedParts[i];
                            if (current.Tag == ContainerStart
                                || current.Tag == ContainerEnd
                                || current.Tag == TextTags.LineBreak)
                            {
                                stringBuilder.Append(formattingOptions.NewLine);

                                if (markdownFormat != MarkdownFormat.AllTextAsCSharp
                                    && markdownFormat != MarkdownFormat.FirstLineDefaultRestCSharp)
                                {
                                    // End the codeblock
                                    stringBuilder.Append("```");

                                    // We know we're at a line break of some kind, but it could be
                                    // a container start, so let the standard handling take care of it.
                                    goto standardHandling;
                                }
                            }
                            else
                            {
                                stringBuilder.Append(current.Text);
                            }
                        }

                        // If we're here, that means that the last part has been reached, so just
                        // return.
                        Debug.Assert(i == taggedParts.Length);
                        stringBuilder.Append(formattingOptions.NewLine);
                        stringBuilder.Append("```");
                        endedWithLineBreak = false;
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
                        brokeLine = false;
                        break;
                }
            }

            if (isInCodeBlock)
            {
                endBlock();
            }

            if (!brokeLine && markdownFormat == MarkdownFormat.Italicize)
            {
                stringBuilder.Append("_");
            }

            endedWithLineBreak = brokeLine;
            return;

            void addText(string text)
            {
                brokeLine = false;
                afterFirstLine = true;
                if (!isInCodeBlock)
                {
                    text = Escape(text);
                }
                stringBuilder.Append(text);
            }

            void addNewline()
            {
                if (isInCodeBlock)
                {
                    endBlock();
                }

                if (markdownFormat == MarkdownFormat.Italicize)
                {
                    stringBuilder.Append("_");
                }

                // Markdown needs 2 linebreaks to make a new paragraph
                stringBuilder.Append(formattingOptions.NewLine);
                stringBuilder.Append(formattingOptions.NewLine);
                brokeLine = true;

                if (markdownFormat == MarkdownFormat.Italicize)
                {
                    stringBuilder.Append("_");
                }
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

    public enum MarkdownFormat
    {
        /// <summary>
        /// Only format entire lines as C# code if there is no standard text on the line
        /// </summary>
        Default,
        /// <summary>
        /// Italicize the section
        /// </summary>
        Italicize,
        /// <summary>
        /// Format the first line as C#, unconditionally
        /// </summary>
        FirstLineAsCSharp,
        /// <summary>
        /// Format the first line as default text, and format the rest of the lines as C#, unconditionally
        /// </summary>
        FirstLineDefaultRestCSharp,
        /// <summary>
        /// Format the entire set of text as C#, unconditionally
        /// </summary>
        AllTextAsCSharp
    }
}
