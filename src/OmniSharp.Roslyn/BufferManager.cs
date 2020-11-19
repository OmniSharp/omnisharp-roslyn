using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Text;
using Microsoft.Extensions.Logging;
using OmniSharp.FileWatching;
using OmniSharp.Models;
using OmniSharp.Models.ChangeBuffer;
using OmniSharp.Models.UpdateBuffer;

namespace OmniSharp.Roslyn
{
    public class BufferManager
    {
        private readonly OmniSharpWorkspace _workspace;
        private readonly IDictionary<string, IEnumerable<DocumentId>> _transientDocuments = new Dictionary<string, IEnumerable<DocumentId>>(StringComparer.OrdinalIgnoreCase);
        private readonly ISet<DocumentId> _transientDocumentIds = new HashSet<DocumentId>();
        private readonly object _lock = new object();
        private readonly IFileSystemWatcher _fileSystemWatcher;
        private readonly ILogger<BufferManager> _logger;

        public BufferManager(OmniSharpWorkspace workspace, ILoggerFactory loggerFactory, IFileSystemWatcher fileSystemWatcher)
        {
            _workspace = workspace;
            _workspace.WorkspaceChanged += OnWorkspaceChanged;
            _fileSystemWatcher = fileSystemWatcher;
            _logger = loggerFactory.CreateLogger<BufferManager>();
        }

        public bool IsTransientDocument(DocumentId documentId)
        {
            lock (_lock)
            {
                return _transientDocumentIds.Contains(documentId);
            }
        }

        public async Task UpdateBufferAsync(Request request, bool isCreate = false)
        {
            var buffer = request.Buffer;
            var changes = request.Changes;

            if (request is UpdateBufferRequest updateRequest && updateRequest.FromDisk)
            {
                buffer = File.ReadAllText(updateRequest.FileName);
                _logger.LogDebug("Read file contents:\n{0}", buffer);
            }

            if (request.FileName == null || (buffer == null && changes == null))
            {
                return;
            }

            var solution = _workspace.CurrentSolution;

            var documentIds = solution.GetDocumentIdsWithFilePath(request.FileName);
            if (!documentIds.IsEmpty)
            {
                if (changes == null)
                {
                    var sourceText = SourceText.From(buffer);

                    foreach (var documentId in documentIds)
                    {
                        var document = solution.GetDocument(documentId);

                        // This can happen when the client sends a create request for a file that we created on the server,
                        // such as RunCodeAction. Unfortunately, previous attempts to have this fully controlled by the vscode
                        // client (such that it sent both create event and then updated existing text) wasn't successful:
                        // vscode seems to always trigger an update buffer event before triggering the create event.
                        if (isCreate && string.IsNullOrEmpty(buffer) && (await document.GetTextAsync()).Length > 0)
                        {
                            _logger.LogDebug("File was created with content in workspace, ignoring disk update");
                            continue;
                        }

                        solution = document.WithText(sourceText).Project.Solution;
                        _logger.LogDebug("Updating file {0} with new text:\n{1}", request.FileName, sourceText);
                    }
                }
                else
                {
                    foreach (var documentId in documentIds)
                    {
                        var document = solution.GetDocument(documentId);
                        var sourceText = await document.GetTextAsync();

                        if (request.ApplyChangesTogether)
                        {
                            var textChanges = new List<TextChange>();
                            foreach (var change in request.Changes)
                            {
                                var startOffset = sourceText.Lines.GetPosition(new LinePosition(change.StartLine, change.StartColumn));
                                var endOffset = sourceText.Lines.GetPosition(new LinePosition(change.EndLine, change.EndColumn));

                                textChanges.Add(new TextChange(new TextSpan(startOffset, endOffset - startOffset), change.NewText));
                            }

                            sourceText = sourceText.WithChanges(textChanges);
                        }
                        else
                        {
                            foreach (var change in request.Changes)
                            {
                                var startOffset = sourceText.Lines.GetPosition(new LinePosition(change.StartLine, change.StartColumn));
                                var endOffset = sourceText.Lines.GetPosition(new LinePosition(change.EndLine, change.EndColumn));

                                sourceText = sourceText.WithChanges(new[] {
                                    new TextChange(new TextSpan(startOffset, endOffset - startOffset), change.NewText)
                                });
                            }
                        }

                        _logger.LogDebug("Updating file {0} with new text:\n{1}", document.FilePath, sourceText);

                        solution = solution.WithDocumentText(documentId, sourceText);
                    }
                }

                _workspace.TryApplyChanges(solution);
            }
            else if (buffer != null)
            {
                _logger.LogDebug("Adding transient file for {0}\n{1}", request.FileName, buffer);
                TryAddTransientDocument(request.FileName, buffer);
            }
        }

        public async Task UpdateBufferAsync(ChangeBufferRequest request)
        {
            if (request.FileName == null)
            {
                return;
            }

            var solution = _workspace.CurrentSolution;

            var documentIds = solution.GetDocumentIdsWithFilePath(request.FileName);
            if (!documentIds.IsEmpty)
            {
                foreach (var documentId in documentIds)
                {
                    var document = solution.GetDocument(documentId);
                    var sourceText = await document.GetTextAsync();

                    var startOffset = sourceText.Lines.GetPosition(new LinePosition(request.StartLine, request.StartColumn));
                    var endOffset = sourceText.Lines.GetPosition(new LinePosition(request.EndLine, request.EndColumn));

                    sourceText = sourceText.WithChanges(new[] {
                        new TextChange(new TextSpan(startOffset, endOffset - startOffset), request.NewText)
                    });

                    solution = solution.WithDocumentText(documentId, sourceText);
                }

                _workspace.TryApplyChanges(solution);
            }
            else
            {
                // TODO@joh ensure the edit is an insert at offset zero
                TryAddTransientDocument(request.FileName, request.NewText);
            }
        }

        private bool TryAddTransientDocument(string fileName, string fileContent)
        {
            if (string.IsNullOrWhiteSpace(fileName))
            {
                return false;
            }

            var projects = FindProjectsByFileName(fileName);
            if (!projects.Any())
            {
                if (fileName.EndsWith(".cs") && _workspace.TryAddMiscellaneousDocument(fileName, LanguageNames.CSharp) != null)
                {
                    _fileSystemWatcher.Watch(fileName, OnFileChanged);
                    return true;
                }

                return false;
            }
            else
            {
                var projectAndDocumentIds = new List<(ProjectId ProjectId, DocumentId DocumentId)>();
                var sourceText = SourceText.From(fileContent);

                foreach (var project in projects)
                {
                    var documentId = DocumentId.CreateNewId(project.Id);
                    projectAndDocumentIds.Add((project.Id, documentId));
                }

                lock (_lock)
                {
                    var documentIds = projectAndDocumentIds.Select(x => x.DocumentId);
                    _transientDocuments[fileName] = documentIds;
                    _transientDocumentIds.UnionWith(documentIds);
                }

                foreach (var projectAndDocumentId in projectAndDocumentIds)
                {
                    var version = VersionStamp.Create();
                    _workspace.AddDocument(projectAndDocumentId.DocumentId, projectAndDocumentId.ProjectId, fileName, TextLoader.From(TextAndVersion.Create(sourceText, version)));
                }
            }

            return true;
        }

        private void OnFileChanged(string filePath, FileChangeType changeType)
        {
            if (changeType == FileChangeType.Unspecified && !File.Exists(filePath) || changeType == FileChangeType.Delete)
            {
                _workspace.TryRemoveMiscellaneousDocument(filePath);
            }
        }

        private IEnumerable<Project> FindProjectsByFileName(string fileName)
        {
            return _workspace.CurrentSolution.Projects
                .Where(project => _workspace.FileBelongsToProject(fileName, project))
                .ToImmutableArray();
        }

        private void OnWorkspaceChanged(object sender, WorkspaceChangeEventArgs args)
        {
            string fileName = null;
            if (args.Kind == WorkspaceChangeKind.DocumentAdded)
            {
                fileName = args.NewSolution.GetDocument(args.DocumentId).FilePath;
            }
            else if (args.Kind == WorkspaceChangeKind.DocumentRemoved)
            {
                fileName = args.OldSolution.GetDocument(args.DocumentId).FilePath;
            }

            if (fileName == null)
            {
                return;
            }

            lock (_lock)
            {
                if (_transientDocumentIds.Contains(args.DocumentId))
                {
                    return;
                }

                if (!_transientDocuments.TryGetValue(fileName, out var documentIds))
                {
                    return;
                }

                _transientDocuments.Remove(fileName);
                foreach (var documentId in documentIds)
                {
                    _workspace.RemoveDocument(documentId);
                    _transientDocumentIds.Remove(documentId);
                }
            }
        }
    }
}
