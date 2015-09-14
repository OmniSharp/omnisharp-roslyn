using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Text;
using OmniSharp.Models;

namespace OmniSharp.Roslyn
{
    public class BufferManager
    {
        private readonly OmnisharpWorkspace _workspace;
        private readonly IDictionary<string, IEnumerable<DocumentId>> _transientDocuments = new Dictionary<string, IEnumerable<DocumentId>>(StringComparer.OrdinalIgnoreCase);
        private readonly ISet<DocumentId> _transientDocumentIds = new HashSet<DocumentId>();
        private readonly object _lock = new object();
        
        public BufferManager(OmnisharpWorkspace workspace)
        {
            _workspace = workspace;
            _workspace.WorkspaceChanged += OnWorkspaceChanged;
        }

        public async Task UpdateBuffer(Request request)
        {
            var buffer = request.Buffer;
            var changes = request.Changes;

            var updateRequest = request as UpdateBufferRequest;
            if (updateRequest != null && updateRequest.FromDisk)
            {
                buffer = File.ReadAllText(updateRequest.FileName);
            }

            if (request.FileName == null || (buffer == null && changes == null))
            {
                return;
            }

            var documentIds = _workspace.CurrentSolution.GetDocumentIdsWithFilePath(request.FileName);
            if (!documentIds.IsEmpty)
            {
                if (changes == null)
                {
                    var sourceText = SourceText.From(buffer);
                    foreach (var documentId in documentIds)
                    {
                        _workspace.OnDocumentChanged(documentId, sourceText);
                    }
                }
                else
                {
                    foreach (var documentId in documentIds)
                    {
                        var document = _workspace.CurrentSolution.GetDocument(documentId);
                        var sourceText = await document.GetTextAsync();

                        foreach (var change in request.Changes)
                        {
                            var startOffset = sourceText.Lines.GetPosition(new LinePosition(change.StartLine - 1, change.StartColumn - 1));
                            var endOffset = sourceText.Lines.GetPosition(new LinePosition(change.EndLine - 1, change.EndColumn - 1));

                            sourceText = sourceText.WithChanges(new[] {
                                new TextChange(new TextSpan(startOffset, endOffset - startOffset), change.NewText)
                            });
                        }
                        _workspace.OnDocumentChanged(documentId, sourceText);
                    }
                }
            }
            else if(buffer != null)
            {
                TryAddTransientDocument(request.FileName, buffer);
            }
        }

        public async Task UpdateBuffer(ChangeBufferRequest request)
        {
            if (request.FileName == null)
            {
                return;
            }

            var documentIds = _workspace.CurrentSolution.GetDocumentIdsWithFilePath(request.FileName);
            if (!documentIds.IsEmpty)
            {
                foreach (var documentId in documentIds)
                {
                    var document = _workspace.CurrentSolution.GetDocument(documentId);
                    var sourceText = await document.GetTextAsync();
                    var startOffset = sourceText.Lines.GetPosition(new LinePosition(request.StartLine - 1, request.StartColumn - 1));
                    var endOffset = sourceText.Lines.GetPosition(new LinePosition(request.EndLine - 1, request.EndColumn - 1));

                    sourceText = sourceText.WithChanges(new[] {
                        new TextChange(new TextSpan(startOffset, endOffset - startOffset), request.NewText)
                    });

                    _workspace.OnDocumentChanged(documentId, sourceText);
                }
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
            if (projects.Count() == 0)
            {
                return false;
            }

            var sourceText = SourceText.From(fileContent);
            var documents = new List<DocumentInfo>();
            foreach (var project in projects)
            {
                var id = DocumentId.CreateNewId(project.Id);
                var version = VersionStamp.Create();
                var document = DocumentInfo.Create(id, fileName, filePath: fileName, loader: TextLoader.From(TextAndVersion.Create(sourceText, version)));

                documents.Add(document);
            }

            lock (_lock)
            {
                var documentIds = documents.Select(document => document.Id);
                _transientDocuments.Add(fileName, documentIds);
                _transientDocumentIds.UnionWith(documentIds);
            }

            foreach (var document in documents)
            {
                _workspace.AddDocument(document);
            }
            return true;
        }

        private IEnumerable<Project> FindProjectsByFileName(string fileName)
        {
            var dirInfo = new FileInfo(fileName).Directory;
            var candidates = _workspace.CurrentSolution.Projects
                .GroupBy(project => new FileInfo(project.FilePath).Directory.FullName)
                .ToDictionary(grouping => grouping.Key, grouping => grouping.ToList());

            List<Project> projects = null;
            while (dirInfo != null)
            {
                if (candidates.TryGetValue(dirInfo.FullName, out projects))
                {
                    return projects;
                }

                dirInfo = dirInfo.Parent;
            }

            return Enumerable.Empty<Project>();
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

                IEnumerable<DocumentId> documentIds;
                if (!_transientDocuments.TryGetValue(fileName, out documentIds))
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
