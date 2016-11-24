using System.Collections.Generic;
using System.Composition.Hosting;
using System.Reflection;
using System.Threading.Tasks;
using OmniSharp.Models;
using OmniSharp.Options;
using OmniSharp.Roslyn.CSharp.Services.Intellisense;
using TestUtility;

namespace OmniSharp.Roslyn.CSharp.Tests
{
    public class AbstractAutoCompleteTests
    {
        private CompositionHost _plugInHost;

        protected AbstractAutoCompleteTests()
        {
            _plugInHost = TestHelpers.CreatePluginHost(new[]
            {
                typeof(IntellisenseService).GetTypeInfo().Assembly
            });
        }

        protected async Task<IEnumerable<AutoCompleteResponse>> FindCompletionsAsync(string input, AutoCompleteRequest request = null, bool wantSnippet = false)
        {
            var markup = MarkupCode.Parse(input);

            var workspace = await TestHelpers.CreateSimpleWorkspace(_plugInHost, markup.Code);
            var controller = new IntellisenseService(workspace, new FormattingOptions());

            if (request == null)
            {
                request = CreateRequest(input, wantSnippet: wantSnippet);
            }

            return await controller.Handle(request);
        }

        protected AutoCompleteRequest CreateRequest(string input, string fileName = "dummy.cs", bool wantSnippet = false)
        {
            var markup = MarkupCode.Parse(input);
            var point = markup.Text.GetPointFromPosition(markup.Position);

            return new AutoCompleteRequest
            {
                Line = point.Line,
                Column = point.Offset,
                FileName = fileName,
                Buffer = markup.Code,
                WordToComplete = GetPartialWord(markup),
                WantMethodHeader = true,
                WantSnippet = wantSnippet,
                WantReturnType = true
            };
        }

        private static string GetPartialWord(MarkupCode markup)
        {
            if (!markup.HasPosition || markup.Position == 0)
            {
                return string.Empty;
            }

            var index = markup.Position;
            while (index >= 1)
            {
                var ch = markup.Code[index - 1];
                if (ch != '_' && !char.IsLetterOrDigit(ch))
                {
                    break;
                }

                index--;
            }

            return markup.Code.Substring(index, markup.Position - index);
        }
    }
}
