using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Text;
using OmniSharp.Models;

namespace OmniSharp
{
    public static class FileChanges
    {
        public static async Task<IEnumerable<ModifiedFileResponse>> GetFileChangesAsync(Solution newSolution, Solution oldSolution, string newFileDirectory, bool wantTextChanges)
        {
            var changes = new Dictionary<string, ModifiedFileResponse>();
            var solutionChanges = newSolution.GetChanges(oldSolution);
            foreach (var projectChange in solutionChanges.GetProjectChanges())
            {
                foreach (var changedDocumentId in projectChange.GetAddedDocuments())
                {
                    var document = newSolution.GetDocument(changedDocumentId);
                    var source = await document.GetTextAsync();
                    var modifiedFileResponse = new ModifiedFileResponse(document.Name);
                    var change = new LinePositionSpanTextChange();
                    change.NewText = source.ToString();
                    var newPath = Path.Combine(newFileDirectory, document.Name);
                    modifiedFileResponse.FileName = newPath;
                    modifiedFileResponse.Changes = new[] { change };
                    changes[newPath] = modifiedFileResponse;

                    // This is a little weird. The added document doesn't have a filepath
                    // and we need one so that future operations on this document work
                    var id = DocumentId.CreateNewId(document.Project.Id);
                    var version = VersionStamp.Create();
                    var documentInfo = DocumentInfo.Create(id, document.Name, filePath: newPath, loader: TextLoader.From(TextAndVersion.Create(source, version)));

                    var workspace = newSolution.Workspace as OmnisharpWorkspace;
                    workspace.RemoveDocument(changedDocumentId);
                    workspace.AddDocument(documentInfo);
                }

                foreach (var changedDocumentId in projectChange.GetChangedDocuments())
                {
                    var changedDocument = newSolution.GetDocument(changedDocumentId);
                    ModifiedFileResponse modifiedFileResponse;
                    var filePath = changedDocument.FilePath;

                    if (!changes.TryGetValue(filePath, out modifiedFileResponse))
                    {
                        modifiedFileResponse = new ModifiedFileResponse(filePath);
                        changes[filePath] = modifiedFileResponse;
                    }

                    if (!wantTextChanges)
                    {
                        var changedText = await changedDocument.GetTextAsync();
                        modifiedFileResponse.Buffer = changedText.ToString();
                    }
                    else
                    {
                        var originalDocument = oldSolution.GetDocument(changedDocumentId);
                        IEnumerable<TextChange> textChanges;
                        textChanges = await changedDocument.GetTextChangesAsync(originalDocument);
                        var linePositionSpanTextChanges = await LinePositionSpanTextChange.Convert(originalDocument, textChanges);

                        modifiedFileResponse.Changes = modifiedFileResponse.Changes != null
                            ? modifiedFileResponse.Changes.Union(linePositionSpanTextChanges)
                            : linePositionSpanTextChanges;
                    }
                }
            }
            return changes.Values;
        }
    }
}
