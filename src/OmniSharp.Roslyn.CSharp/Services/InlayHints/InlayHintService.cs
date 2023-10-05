using System.Collections.Generic;
using System.Collections.Immutable;
using System.Composition;
using System.Diagnostics.CodeAnalysis;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.ExternalAccess.OmniSharp.InlineHints;
using Microsoft.CodeAnalysis.Text;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using OmniSharp.Extensions;
using OmniSharp.Mef;
using OmniSharp.Models;
using OmniSharp.Models.v1.InlayHints;
using OmniSharp.Options;
using OmniSharp.Roslyn.CSharp.Helpers;
using OmniSharp.Roslyn.Utilities;

#nullable enable

namespace OmniSharp.Roslyn.CSharp.Services.InlayHints;

[Shared]
[OmniSharpHandler(OmniSharpEndpoints.InlayHint, LanguageNames.CSharp)]
[OmniSharpHandler(OmniSharpEndpoints.InlayHintResolve, LanguageNames.CSharp)]
internal class InlayHintService :
    IRequestHandler<InlayHintRequest, InlayHintResponse>,
    IRequestHandler<InlayHintResolveRequest, InlayHint>
{
    private readonly OmniSharpWorkspace _workspace;
    private readonly IOptionsMonitor<OmniSharpOptions> _omniSharpOptions;
    private readonly ILogger _logger;
    private readonly InlineHintCache _cache;
    private readonly FormattingOptions _formattingOptions;

    private const double ParameterRanking = 0.0;

    [ImportingConstructor]
    public InlayHintService(OmniSharpWorkspace workspace, FormattingOptions formattingOptions, ILoggerFactory loggerFactory, IOptionsMonitor<OmniSharpOptions> omniSharpOptions)
    {
        _workspace = workspace;
        _formattingOptions = formattingOptions;
        _logger = loggerFactory.CreateLogger<InlayHintService>();
        _omniSharpOptions = omniSharpOptions;
        _cache = new(_logger);
    }

    public async Task<InlayHintResponse> Handle(InlayHintRequest request)
    {
        var document = _workspace.GetDocument(request.Location.FileName);
        if (document == null)
        {
            _logger.Log(LogLevel.Warning, $"Inlay hints requested for document not in workspace {request.Location}");
            return InlayHintResponse.None;
        }

        var sourceText = await document.GetTextAsync();
        var mappedSpan = sourceText.GetSpanFromRange(request.Location.Range);

        var inlayHintsOptions = _omniSharpOptions.CurrentValue.RoslynExtensionsOptions.InlayHintsOptions;
        var options = new OmniSharpInlineHintsOptions
        {
            ParameterOptions = new()
            {
                EnabledForParameters = inlayHintsOptions.EnableForParameters,
                ForIndexerParameters = inlayHintsOptions.ForIndexerParameters,
                ForLiteralParameters = inlayHintsOptions.ForLiteralParameters,
                ForObjectCreationParameters = inlayHintsOptions.ForObjectCreationParameters,
                ForOtherParameters = inlayHintsOptions.ForOtherParameters,
                SuppressForParametersThatDifferOnlyBySuffix = inlayHintsOptions.SuppressForParametersThatDifferOnlyBySuffix,
                SuppressForParametersThatMatchArgumentName = inlayHintsOptions.SuppressForParametersThatMatchArgumentName,
                SuppressForParametersThatMatchMethodIntent = inlayHintsOptions.SuppressForParametersThatMatchMethodIntent,
            },
            TypeOptions = new()
            {
                EnabledForTypes = inlayHintsOptions.EnableForTypes,
                ForImplicitObjectCreation = inlayHintsOptions.ForImplicitObjectCreation,
                ForImplicitVariableTypes = inlayHintsOptions.ForImplicitVariableTypes,
                ForLambdaParameterTypes = inlayHintsOptions.ForLambdaParameterTypes,
            }
        };

        var hints = await OmniSharpInlineHintsService.GetInlineHintsAsync(document, mappedSpan, options, CancellationToken.None);

        var solutionVersion = _workspace.CurrentSolution.Version;

        return new()
        {
            InlayHints = _cache.MapAndCacheHints(hints, document, solutionVersion, sourceText)
        };
    }

    public async Task<InlayHint> Handle(InlayHintResolveRequest request)
    {
        if (!_cache.TryGetFromCache(request.Hint, out var roslynHint, out var document))
        {
            return request.Hint;
        }

        var descriptionTags = await roslynHint.GetDescriptionAsync(document, CancellationToken.None);
        StringBuilder stringBuilder = new StringBuilder();
        MarkdownHelpers.TaggedTextToMarkdown(
            descriptionTags,
            stringBuilder,
            _formattingOptions,
            MarkdownFormat.FirstLineAsCSharp,
            out _);

        return request.Hint with
        {
            Tooltip = stringBuilder.ToString(),
        };
    }

    private class InlineHintCache
    {
        private readonly object _lock = new();
        private string? _currentVersionString;
        private List<(OmniSharpInlineHint Hint, Document Document)>? _hints;
        private readonly ILogger _logger;

        public InlineHintCache(ILogger logger)
        {
            _logger = logger;
        }

        public List<InlayHint> MapAndCacheHints(ImmutableArray<OmniSharpInlineHint> roslynHints, Document document, VersionStamp solutionVersion, SourceText text)
        {
            var resultList = new List<InlayHint>();
            var solutionVersionString = solutionVersion.ToString();
            lock (_lock)
            {
                var hintsList = _currentVersionString == solutionVersionString
                    ? _hints
                    : new();

                foreach (var hint in roslynHints)
                {
                    var position = hintsList!.Count;
                    resultList.Add(new InlayHint()
                    {
                        Label = string.Concat(hint.DisplayParts),
                        Kind = hint.Ranking == ParameterRanking
                            ? InlayHintKind.Parameter
                            : InlayHintKind.Type,
                        Position = text.GetPointFromPosition(hint.Span.End),
                        TextEdits = ConvertToTextChanges(hint.ReplacementTextChange, text),
                        Data = (solutionVersionString, position)
                    });

                    hintsList.Add((hint, document));
                }

                _currentVersionString = solutionVersionString;
                _hints = hintsList;
            }

            return resultList;
        }

        internal static LinePositionSpanTextChange[]? ConvertToTextChanges(TextChange? textChange, SourceText sourceText)
        {
            return textChange.HasValue
                ? new[] { TextChanges.Convert(sourceText, textChange.Value) }
                : null;
        }

        public bool TryGetFromCache(InlayHint hint, out OmniSharpInlineHint roslynHint, [NotNullWhen(true)] out Document? document)
        {
            (roslynHint, document) = (default, null);
            lock (_lock)
            {
                if (_hints is null)
                {
                    _logger.LogWarning("Attempted to resolve hint before hints were requested");
                    return false;
                }

                if (_currentVersionString == hint.Data.SolutionVersion)
                {
                    if (hint.Data.Position >= _hints.Count)
                    {
                        _logger.LogWarning("Hint position is not found in the list");
                        roslynHint = default;
                        return false;
                    }

                    (roslynHint, document) = _hints[hint.Data.Position];
                    return true;
                }
                else
                {
                    _logger.LogInformation("Requested hint for outdated solution version");
                    roslynHint = default;
                    return false;
                }
            }
        }
    }
}
