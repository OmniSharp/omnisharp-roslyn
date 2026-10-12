using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Text;
using Microsoft.Extensions.Logging;
using OmniSharp.Models.v1.Completion;
using OmniSharp.Roslyn.CSharp.Services.Completion;
using TestUtility;
using Xunit;

namespace OmniSharp.Roslyn.CSharp.Tests
{
    public abstract class CompletionTestBase : AbstractTestFixture
    {
        protected const int ImportCompletionTimeout = 2000;
        protected string EndpointName => OmniSharpEndpoints.Completion;

        protected CompletionTestBase(ITestOutputHelper output, SharedOmniSharpHostFixture sharedOmniSharpHostFixture)
            : base(output, sharedOmniSharpHostFixture)
        {
        }

        protected CompletionService GetCompletionService(OmniSharpTestHost host)
            => host.GetRequestHandler<CompletionService>(EndpointName);

        protected async Task<CompletionResponse> FindCompletionsAsync(string filename, string source, OmniSharpTestHost testHost, char? triggerChar = null, TestFile[] additionalFiles = null, bool forceExpandedCompletionIndexCreation = false)
        {
            var testFile = new TestFile(filename, NormalizeNewlines(source));

            var files = new[] { testFile };
            if (additionalFiles is object)
            {
                files = files.Concat(additionalFiles).ToArray();
            }

            testHost.AddFilesToWorkspace(files);
            var point = testFile.Content.GetPointFromPosition();

            var request = new CompletionRequest
            {
                Line = point.Line,
                Column = point.Offset,
                FileName = testFile.FileName,
                Buffer = testFile.Content.Code,
                CompletionTrigger = triggerChar is object ? CompletionTriggerKind.TriggerCharacter : CompletionTriggerKind.Invoked,
                TriggerCharacter = triggerChar
            };

            var requestHandler = GetCompletionService(testHost);

            return await requestHandler.Handle(request, forceExpandedCompletionIndexCreation);
        }

        protected Task<CompletionResponse> FindCompletionsWithImportedAsync(string filename, string source, OmniSharpTestHost host)
            => FindCompletionsAsync(filename, source, host, forceExpandedCompletionIndexCreation: true);

        protected async Task<CompletionResolveResponse> ResolveCompletionAsync(CompletionItem completionItem, OmniSharpTestHost testHost)
            => await GetCompletionService(testHost).Handle(new CompletionResolveRequest { Item = completionItem });

        protected OmniSharpTestHost GetImportCompletionHost()
        {
            var testHost = CreateOmniSharpHost(configurationData: new[] { new KeyValuePair<string, string>("RoslynExtensionsOptions:EnableImportCompletion", "true") });
            testHost.AddFilesToWorkspace();
            return testHost;
        }

        protected OmniSharpTestHost GetAsyncCompletionAndImportCompletionHost()
        {
            var testHost = CreateOmniSharpHost(configurationData: new[] {
                new KeyValuePair<string, string>("RoslynExtensionsOptions:EnableImportCompletion", "true"),
                new KeyValuePair<string, string>("RoslynExtensionsOptions:EnableAsyncCompletion", "true"),
            });
            testHost.AddFilesToWorkspace();
            return testHost;
        }

        protected Task<CompletionAfterInsertResponse> AfterInsertResponse(CompletionItem completionItem, OmniSharpTestHost testHost)
        {
            return GetCompletionService(testHost).Handle(new CompletionAfterInsertRequest
            {
                Item = completionItem
            });
        }

        protected static string NormalizeNewlines(string str)
            => str.Replace("\r\n", "\n");

        protected static void VerifySortOrders(IReadOnlyList<CompletionItem> items)
        {
            Assert.All(items, c =>
            {
                Assert.True(c.SortText.StartsWith("0") || c.SortText.StartsWith("1"));
            });
        }
    }

    internal static class CompletionResponseExtensions
    {
        public static bool IsSuggestionMode(this CompletionItem item) => !item.CommitCharacters?.Contains(' ') ?? true;

    }
}
