using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using OmniSharp.Models;
using OmniSharp.Models.v1.InlayHints;
using OmniSharp.Models.V2;
using OmniSharp.Options;
using Roslyn.Test.Utilities;
using TestUtility;
using Xunit;

namespace OmniSharp.Roslyn.CSharp.Tests;

public abstract class InlayHintsTestBase : AbstractTestFixture
{
    protected InlayHintsTestBase(ITestOutputHelper output, SharedOmniSharpHostFixture sharedOmniSharpHostFixture)
        : base(output, sharedOmniSharpHostFixture)
    {
    }

    protected static Task<InlayHintResponse> GetInlayHints(string fileName, string code, OmniSharpTestHost testHost)
    {
        var testFile = new TestFile(fileName, code);
        var range = testFile.Content.GetRangeFromSpan(testFile.Content.GetSpans("ihRegion").Single()).GetSelection();

        testHost.AddFilesToWorkspace(testFile);

        return testHost.GetResponse<InlayHintRequest, InlayHintResponse>(OmniSharpEndpoints.InlayHint, new() { Location = new() { FileName = fileName, Range = range } });
    }

    protected static Task<InlayHint> ResolveInlayHint(InlayHint hint, OmniSharpTestHost testHost)
        => testHost.GetResponse<InlayHintResolveRequest, InlayHint>(OmniSharpEndpoints.InlayHintResolve, new() { Hint = hint });

    protected KeyValuePair<string, string>[] InlayHintsOptionsToKvp(InlayHintsOptions options)
        => new[]
        {
            new KeyValuePair<string, string>($"{nameof(RoslynExtensionsOptions)}:{nameof(InlayHintsOptions)}:{nameof(InlayHintsOptions.EnableForParameters)}", options.EnableForParameters.ToString()),
            new KeyValuePair<string, string>($"{nameof(RoslynExtensionsOptions)}:{nameof(InlayHintsOptions)}:{nameof(InlayHintsOptions.ForLiteralParameters)}", options.ForLiteralParameters.ToString()),
            new KeyValuePair<string, string>($"{nameof(RoslynExtensionsOptions)}:{nameof(InlayHintsOptions)}:{nameof(InlayHintsOptions.ForIndexerParameters)}", options.ForIndexerParameters.ToString()),
            new KeyValuePair<string, string>($"{nameof(RoslynExtensionsOptions)}:{nameof(InlayHintsOptions)}:{nameof(InlayHintsOptions.ForObjectCreationParameters)}", options.ForObjectCreationParameters.ToString()),
            new KeyValuePair<string, string>($"{nameof(RoslynExtensionsOptions)}:{nameof(InlayHintsOptions)}:{nameof(InlayHintsOptions.ForOtherParameters)}", options.ForOtherParameters.ToString()),
            new KeyValuePair<string, string>($"{nameof(RoslynExtensionsOptions)}:{nameof(InlayHintsOptions)}:{nameof(InlayHintsOptions.SuppressForParametersThatDifferOnlyBySuffix)}", options.SuppressForParametersThatDifferOnlyBySuffix.ToString()),
            new KeyValuePair<string, string>($"{nameof(RoslynExtensionsOptions)}:{nameof(InlayHintsOptions)}:{nameof(InlayHintsOptions.SuppressForParametersThatMatchMethodIntent)}", options.SuppressForParametersThatMatchMethodIntent.ToString()),
            new KeyValuePair<string, string>($"{nameof(RoslynExtensionsOptions)}:{nameof(InlayHintsOptions)}:{nameof(InlayHintsOptions.SuppressForParametersThatMatchArgumentName)}", options.SuppressForParametersThatMatchArgumentName.ToString()),
            new KeyValuePair<string, string>($"{nameof(RoslynExtensionsOptions)}:{nameof(InlayHintsOptions)}:{nameof(InlayHintsOptions.EnableForTypes)}", options.EnableForTypes.ToString()),
            new KeyValuePair<string, string>($"{nameof(RoslynExtensionsOptions)}:{nameof(InlayHintsOptions)}:{nameof(InlayHintsOptions.ForImplicitVariableTypes)}", options.ForImplicitVariableTypes.ToString()),
            new KeyValuePair<string, string>($"{nameof(RoslynExtensionsOptions)}:{nameof(InlayHintsOptions)}:{nameof(InlayHintsOptions.ForLambdaParameterTypes)}", options.ForLambdaParameterTypes.ToString()),
            new KeyValuePair<string, string>($"{nameof(RoslynExtensionsOptions)}:{nameof(InlayHintsOptions)}:{nameof(InlayHintsOptions.ForImplicitObjectCreation)}", options.ForImplicitObjectCreation.ToString()),
        };
}
