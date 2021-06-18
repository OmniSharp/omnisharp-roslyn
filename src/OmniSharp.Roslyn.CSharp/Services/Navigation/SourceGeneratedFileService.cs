#nullable enable

using Microsoft.CodeAnalysis;
using Microsoft.Extensions.Logging;
using OmniSharp.Mef;
using OmniSharp.Models.v1.SourceGeneratedFile;
using System.Collections.Generic;
using System.Composition;
using System.Threading;
using System.Threading.Tasks;

namespace OmniSharp.Roslyn.CSharp.Services.Navigation
{
    [Shared]
    [OmniSharpHandler(OmniSharpEndpoints.SourceGeneratedFile, LanguageNames.CSharp)]
    [OmniSharpHandler(OmniSharpEndpoints.UpdateSourceGeneratedFile, LanguageNames.CSharp)]
    [OmniSharpHandler(OmniSharpEndpoints.SourceGeneratedFileClosed, LanguageNames.CSharp)]
    public class SourceGeneratedFileService :
        IRequestHandler<SourceGeneratedFileRequest, SourceGeneratedFileResponse>,
        IRequestHandler<UpdateSourceGeneratedFileRequest, UpdateSourceGeneratedFileResponse>,
        IRequestHandler<SourceGeneratedFileClosedRequest, SourceGeneratedFileClosedResponse>
    {
        private readonly OmniSharpWorkspace _workspace;
        private readonly ILogger _logger;
        private readonly Dictionary<DocumentId, VersionStamp> _lastSentVerisons = new();
        private readonly object _lock = new();

        [ImportingConstructor]
        public SourceGeneratedFileService(OmniSharpWorkspace workspace, ILoggerFactory loggerFactory)
        {
            _workspace = workspace;
            _logger = loggerFactory.CreateLogger<SourceGeneratedFileService>();
        }

        public async Task<SourceGeneratedFileResponse> Handle(SourceGeneratedFileRequest request)
        {
            var documentId = GetId(request);

            var document = await _workspace.CurrentSolution.GetSourceGeneratedDocumentAsync(documentId, CancellationToken.None);

            if (document is null)
            {
                _logger.LogError("Document with ID {0}:{1} was not found or not a source generated file", request.ProjectGuid, request.DocumentGuid);
                return new SourceGeneratedFileResponse();
            }

            var text = await document.GetTextAsync();

            var documentVerison = await document.GetTextVersionAsync();
            lock (_lock)
            {
                _lastSentVerisons[documentId] = documentVerison;
            }

            return new SourceGeneratedFileResponse
            {
                Source = text.ToString(),
                SourceName = document.FilePath
            };
        }

        public async Task<UpdateSourceGeneratedFileResponse> Handle(UpdateSourceGeneratedFileRequest request)
        {
            var documentId = GetId(request);
            var document = await _workspace.CurrentSolution.GetSourceGeneratedDocumentAsync(documentId, CancellationToken.None);
            if (document == null)
            {
                lock (_lock)
                {
                    _ = _lastSentVerisons.Remove(documentId);
                }
                return new UpdateSourceGeneratedFileResponse() { UpdateType = UpdateType.Deleted };
            }

            var docVersion = await document.GetTextVersionAsync();
            lock (_lock)
            {
                if (_lastSentVerisons.TryGetValue(documentId, out var lastVersion) && lastVersion == docVersion)
                {
                    return new UpdateSourceGeneratedFileResponse() { UpdateType = UpdateType.Unchanged };
                }

                _lastSentVerisons[documentId] = docVersion;
            }

            return new UpdateSourceGeneratedFileResponse()
            {
                UpdateType = UpdateType.Modified,
                Source = (await document.GetTextAsync()).ToString()
            };
        }

        public Task<SourceGeneratedFileClosedResponse> Handle(SourceGeneratedFileClosedRequest request)
        {
            lock (_lock)
            {
                _ = _lastSentVerisons.Remove(GetId(request));
            }

            return SourceGeneratedFileClosedResponse.Instance;
        }

        private DocumentId GetId(SourceGeneratedFileInfo info) => DocumentId.CreateFromSerialized(ProjectId.CreateFromSerialized(info.ProjectGuid), info.DocumentGuid);
    }
}
