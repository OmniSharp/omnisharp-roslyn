using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Text;
using Microsoft.Extensions.Configuration;
using OmniSharp.Extensions.LanguageServer.Protocol.Document;
using OmniSharp.Extensions.LanguageServer.Protocol.Models;
using TestUtility;
using Xunit;

namespace OmniSharp.Lsp.Tests
{
    public abstract class OmnisharpCompletionTestBase : AbstractLanguageServerTestBase
    {
        protected const int ImportCompletionTimeout = 5000;

        protected OmnisharpCompletionTestBase(ITestOutputHelper output, IConfiguration configuration = null)
            : base(output, configuration)
        {
        }

        protected async Task<CompletionList> FindCompletionsAsync(string filename, string source, char? triggerChar = null, TestFile[] additionalFiles = null)
        {
            var bufferPath = $"{Directory.GetCurrentDirectory()}{Path.DirectorySeparatorChar}somepath{Path.DirectorySeparatorChar}{filename}";
            var testFile = new TestFile(bufferPath, NormalizeNewlines(source));

            var files = new[] { testFile };
            if (additionalFiles is object)
            {
                files = files.Concat(additionalFiles).ToArray();
            }

            OmniSharpTestHost.AddFilesToWorkspace(files);
            var point = testFile.Content.GetPointFromPosition();

            var request = new CompletionParams
            {
                Context = new CompletionContext
                {
                    TriggerKind = triggerChar is object ? CompletionTriggerKind.TriggerCharacter : CompletionTriggerKind.Invoked,
                    TriggerCharacter = triggerChar.ToString()
                },
                Position = new Position
                {
                    Line = point.Line,
                    Character = point.Offset
                },
                TextDocument = new TextDocumentIdentifier(bufferPath)
            };

            return await Client.RequestCompletion(request);
        }

        protected async Task<CompletionList> FindCompletionsWithImportedAsync(string filename, string source)
        {
            var completions = await FindCompletionsAsync(filename, source);
            if (!completions.IsIncomplete)
            {
                return completions;
            }

            // Populating the completion cache should take no more than a few ms, don't let it take too
            // long
            CancellationTokenSource cts = new CancellationTokenSource(millisecondsDelay: ImportCompletionTimeout);
            await Task.Run(async () =>
            {
                while (completions.IsIncomplete)
                {
                    await Task.Delay(100);
                    completions = await FindCompletionsAsync(filename, source);
                    cts.Token.ThrowIfCancellationRequested();
                }
            }, cts.Token);

            Assert.False(completions.IsIncomplete);
            return completions;
        }

        protected async Task<CompletionItem> ResolveCompletionAsync(CompletionItem completionItem)
        {
            return await Client.ResolveCompletion(completionItem);
        }

        protected static string NormalizeNewlines(string str)
            => str.Replace("\r\n", "\n");

        protected static void VerifySortOrders(IEnumerable<CompletionItem> items)
        {
            Assert.All(items, c =>
            {
                Assert.True(c.SortText.StartsWith("0") || c.SortText.StartsWith("1"));
            });
        }
    }

    internal static class CompletionResponseExtensions
    {
        public static bool IsSuggestionMode(this CompletionItem item) => !item.CommitCharacters?.Contains(" ") ?? true;
    }
}
