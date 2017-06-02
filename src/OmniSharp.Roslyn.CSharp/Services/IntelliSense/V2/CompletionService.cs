using System;
using System.Composition;
using System.Threading.Tasks;
using Microsoft.CodeAnalysis;
using Microsoft.Extensions.Logging;
using OmniSharp.Mef;
using CSharpCompletionService = Microsoft.CodeAnalysis.Completion.CompletionService;

namespace OmniSharp.Roslyn.CSharp.Services.IntelliSense.V2
{
    using Models = OmniSharp.Models.V2.Completion;

    [OmniSharpHandler(OmniSharpEndpoints.V2.Completion, LanguageNames.CSharp)]
    internal class CompletionService : IRequestHandler<Models.CompletionRequest, Models.CompletionResponse>
    {
        private readonly OmniSharpWorkspace _workspace;
        private readonly ILogger _logger;

        [ImportingConstructor]
        public CompletionService(OmniSharpWorkspace workspace, ILoggerFactory loggerFactory)
        {
            _workspace = workspace;
            _logger = loggerFactory.CreateLogger<CompletionService>();
        }

        public async Task<Models.CompletionResponse> Handle(Models.CompletionRequest request)
        {
            var fileName = request.FileName;
            var position = request.Position;
            var trigger = request.Trigger.ToRoslynCompletionTrigger();

            var document = _workspace.GetDocument(fileName);
            if (document == null)
            {
                throw new ArgumentException($"Could not find document for {fileName}.");
            }

            var text = await document.GetTextAsync();
            if (position < 0 || position > text.Length)
            {
                throw new ArgumentOutOfRangeException($"Invalid position: {position}. Should be with range 0 to {text.Length}");
            }

            var service = CSharpCompletionService.GetService(document);
            if (service == null)
            {
                throw new InvalidOperationException("Could not retrieve Roslyn CompletionService.");
            }

            if (!service.ShouldTriggerCompletion(text, position, trigger))
            {
                return Models.CompletionResponse.Empty;
            }

            var completionList = await service.GetCompletionsAsync(document, position, trigger);

            var isSuggestionMode = completionList.SuggestionModeItem != null;

            int itemCount = completionList.Items.Length;
            var items = new Models.CompletionItem[itemCount];

            for (int i = 0; i < itemCount; i++)
            {
                var item = completionList.Items[i];

                items[i] = new Models.CompletionItem
                {
                    DisplayText = item.DisplayText,
                    Kind = item.GetKind(),
                    FilterText = item.FilterText,
                    SortText = item.SortText
                };
            }

            return new Models.CompletionResponse
            {
                IsSuggestionMode = isSuggestionMode,
                Items = items
            };
        }
    }
}
