using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using OmniSharp.Extensions.LanguageServer.Protocol.Document;
using OmniSharp.Extensions.LanguageServer.Protocol.Models;
using OmniSharp.Options;
using Roslyn.Test.Utilities;
using TestUtility;
using Xunit;

namespace OmniSharp.Lsp.Tests;

public abstract class OmnisharpInlayHintTestBase : AbstractLanguageServerTestBase
{
    protected static readonly IgnoreDataComparer ignoreDataComparer = new IgnoreDataComparer();

    protected OmnisharpInlayHintTestBase(ITestOutputHelper output) : base(output)
    {
    }

    protected async Task<InlayHintContainer> GetInlayHints(string filename, string source)
    {
        var bufferPath = $"{Directory.GetCurrentDirectory()}{Path.DirectorySeparatorChar}somepath{Path.DirectorySeparatorChar}{filename}";
        var testFile = new TestFile(bufferPath, source);

        OmniSharpTestHost.AddFilesToWorkspace(new[] { testFile });

        var range = testFile.Content.GetRangeFromSpan(testFile.Content.GetSpans("ihRegion").Single()).GetSelection();

        var request = new InlayHintParams
        {
            TextDocument = new TextDocumentIdentifier(bufferPath),
            Range = new Range()
            {
                Start = new Position()
                {
                    Line = range.Start.Line,
                    Character = range.Start.Column
                },
                End = new Position()
                {
                    Line = range.End.Line,
                    Character = range.End.Column
                }
            }
        };

        return await Client.RequestInlayHints(request);
    }

    protected async Task<InlayHint> ResolveInlayHint(InlayHint inlayHint)
    {
        return await Client.ResolveInlayHint(inlayHint);
    }

    protected async Task SetInlayHintOptionsAsync(InlayHintsOptions options)
    {
        await Restart(configurationData: InlayHintsOptionsToDictionary(options));
    }

    private Dictionary<string, string> InlayHintsOptionsToDictionary(InlayHintsOptions options)
        => new()
        {
            [$"{nameof(RoslynExtensionsOptions)}:{nameof(InlayHintsOptions)}:{nameof(InlayHintsOptions.EnableForParameters)}"] = options.EnableForParameters.ToString(),
            [$"{nameof(RoslynExtensionsOptions)}:{nameof(InlayHintsOptions)}:{nameof(InlayHintsOptions.ForLiteralParameters)}"] = options.ForLiteralParameters.ToString(),
            [$"{nameof(RoslynExtensionsOptions)}:{nameof(InlayHintsOptions)}:{nameof(InlayHintsOptions.ForIndexerParameters)}"] = options.ForIndexerParameters.ToString(),
            [$"{nameof(RoslynExtensionsOptions)}:{nameof(InlayHintsOptions)}:{nameof(InlayHintsOptions.ForObjectCreationParameters)}"] = options.ForObjectCreationParameters.ToString(),
            [$"{nameof(RoslynExtensionsOptions)}:{nameof(InlayHintsOptions)}:{nameof(InlayHintsOptions.ForOtherParameters)}"] = options.ForOtherParameters.ToString(),
            [$"{nameof(RoslynExtensionsOptions)}:{nameof(InlayHintsOptions)}:{nameof(InlayHintsOptions.SuppressForParametersThatDifferOnlyBySuffix)}"] = options.SuppressForParametersThatDifferOnlyBySuffix.ToString(),
            [$"{nameof(RoslynExtensionsOptions)}:{nameof(InlayHintsOptions)}:{nameof(InlayHintsOptions.SuppressForParametersThatMatchMethodIntent)}"] = options.SuppressForParametersThatMatchMethodIntent.ToString(),
            [$"{nameof(RoslynExtensionsOptions)}:{nameof(InlayHintsOptions)}:{nameof(InlayHintsOptions.SuppressForParametersThatMatchArgumentName)}"] = options.SuppressForParametersThatMatchArgumentName.ToString(),
            [$"{nameof(RoslynExtensionsOptions)}:{nameof(InlayHintsOptions)}:{nameof(InlayHintsOptions.EnableForTypes)}"] = options.EnableForTypes.ToString(),
            [$"{nameof(RoslynExtensionsOptions)}:{nameof(InlayHintsOptions)}:{nameof(InlayHintsOptions.ForImplicitVariableTypes)}"] = options.ForImplicitVariableTypes.ToString(),
            [$"{nameof(RoslynExtensionsOptions)}:{nameof(InlayHintsOptions)}:{nameof(InlayHintsOptions.ForLambdaParameterTypes)}"] = options.ForLambdaParameterTypes.ToString(),
            [$"{nameof(RoslynExtensionsOptions)}:{nameof(InlayHintsOptions)}:{nameof(InlayHintsOptions.ForImplicitObjectCreation)}"] = options.ForImplicitObjectCreation.ToString(),
        };

    protected sealed class IgnoreDataComparer : IEqualityComparer<InlayHint>
    {
        public bool Equals(InlayHint x, InlayHint y)
        {
            if (ReferenceEquals(x, y)) return true;
            if (x is null) return false;
            if (y is null) return false;

            return x.Position == y.Position
                && x.Label == y.Label
                && x.Tooltip == y.Tooltip
                && TextEditsEqual(x.TextEdits?.ToArray(), y.TextEdits?.ToArray())
                && x.PaddingLeft == y.PaddingLeft
                && x.PaddingRight == y.PaddingRight;
        }

        private static bool TextEditsEqual(TextEdit[] a, TextEdit[] b)
        {
            if (a is null)
            {
                return b is null;
            }

            if (b is null)
            {
                return false;
            }

            if (a.Length != b.Length)
            {
                return false;
            }

            for (int index = 0; index < a.Length; index++)
            {
                if (!a[index].Equals(b[index]))
                {
                    return false;
                }
            }

            return true;
        }

        public int GetHashCode(InlayHint x)
            => (x.Position, x.Label, x.Tooltip, x.TextEdits?.GetHashCode() ?? 0, x.PaddingLeft, x.PaddingRight).GetHashCode();
    }
}
