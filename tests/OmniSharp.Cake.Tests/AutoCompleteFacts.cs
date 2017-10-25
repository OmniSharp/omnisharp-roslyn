using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using OmniSharp.Cake.Services.RequestHandlers.Intellisense;
using OmniSharp.Models.AutoComplete;
using OmniSharp.Models.UpdateBuffer;
using TestUtility;
using Xunit;
using Xunit.Abstractions;

namespace OmniSharp.Cake.Tests
{
    public class AutoCompleteFacts : CakeSingleRequestHandlerTestFixture<AutoCompleteHandler>
    {
        private readonly ILogger _logger;

        public AutoCompleteFacts(ITestOutputHelper testOutput) : base(testOutput)
        {
            _logger = LoggerFactory.CreateLogger<AutoCompleteFacts>();
        }

        protected override string EndpointName => OmniSharpEndpoints.AutoComplete;

        [Fact]
        public async Task ShouldGenerateFromHostObject()
        {
            const string input = @"Task$$";

            var completions = await FindCompletionsAsync(input, wantSnippet: true);
            ContainsCompletions(completions.Select(c => c.DisplayText).Take(1), "Task(string name)");
        }


        private async Task<IEnumerable<AutoCompleteResponse>> FindCompletionsAsync(string source, bool wantSnippet = false)
        {
            using (var testProject = await TestAssets.Instance.GetTestProjectAsync("CakeProject", shadowCopy : false))
            using (var host = CreateOmniSharpHost(testProject.Directory))
            {
                var testFile = new TestFile(Path.Combine(testProject.Directory, "build.cake"), source);
                var point = testFile.Content.GetPointFromPosition();

                var request = new AutoCompleteRequest
                {
                    Line = point.Line,
                    Column = point.Offset,
                    FileName = testFile.FileName,
                    Buffer = testFile.Content.Code,
                    WordToComplete = GetPartialWord(testFile.Content),
                    WantMethodHeader = true,
                    WantSnippet = wantSnippet,
                    WantReturnType = true
                };

                var updateBufferRequest = new UpdateBufferRequest
                {
                    Buffer = request.Buffer,
                    Column = request.Column,
                    FileName = request.FileName,
                    Line = request.Line,
                    FromDisk = false
                };

                await GetUpdateBufferHandler(host).Handle(updateBufferRequest);

                var requestHandler = GetRequestHandler(host);

                return await requestHandler.Handle(request);
            }
        }

        private static string GetPartialWord(TestContent testConnect)
        {
            if (!testConnect.HasPosition || testConnect.Position == 0)
            {
                return string.Empty;
            }

            var index = testConnect.Position;
            while (index >= 1)
            {
                var ch = testConnect.Code[index - 1];
                if (ch != '_' && !char.IsLetterOrDigit(ch))
                {
                    break;
                }

                index--;
            }

            return testConnect.Code.Substring(index, testConnect.Position - index);
        }

        private void ContainsCompletions(IEnumerable<string> completions, params string[] expected)
        {
            if (!completions.SequenceEqual(expected))
            {
                var builder = new StringBuilder();
                builder.AppendLine("Expected");
                builder.AppendLine("--------");

                foreach (var completion in expected)
                {
                    builder.AppendLine(completion);
                }

                builder.AppendLine();
                builder.AppendLine("Found");
                builder.AppendLine("-----");

                foreach (var completion in completions)
                {
                    builder.AppendLine(completion);
                }

                _logger.LogError(builder.ToString());
            }

            Assert.Equal(expected, completions.ToArray());
        }
    }
}
