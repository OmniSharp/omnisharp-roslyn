using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Text;
using OmniSharp.Models;

namespace OmniSharp
{
    public static class FileChanges
    {
        public static async Task<IEnumerable<ModifiedFileResponse>> GetFileChangesAsync(Solution newSolution, Solution oldSolution, bool wantTextChanges)
        {
            var changes = new Dictionary<string, ModifiedFileResponse>();
            var solutionChanges = oldSolution.GetChanges(newSolution);
            foreach (var projectChange in solutionChanges.GetProjectChanges())
            {
                foreach (var changedDocumentId in projectChange.GetAddedDocuments())
                {
                    var document = oldSolution.GetDocument(changedDocumentId);
                    var source = await document.GetTextAsync();
                    var modifiedFileResponse = new ModifiedFileResponse(document.Name);
                    var change = new LinePositionSpanTextChange();
                    change.NewText = source.ToString();
                    modifiedFileResponse.FileName = document.Name;
                    modifiedFileResponse.Changes = new[] { change };
                    changes[document.Name] = modifiedFileResponse;
                    // This is a little weird. The added document doesn't have a filepath
                    // and we need one so that future operations on this document work

                    var id = DocumentId.CreateNewId(document.Project.Id);
                    var version = VersionStamp.Create();
                    var documentInfo = DocumentInfo.Create(id, document.Name, filePath: document.Name, loader: TextLoader.From(TextAndVersion.Create(source, version)));

                    var workspace = newSolution.Workspace as OmnisharpWorkspace;
                    workspace.AddDocument(documentInfo);
                    workspace.RemoveDocument(changedDocumentId);
                }

                foreach (var changedDocumentId in projectChange.GetChangedDocuments())
                {
                    var changedDocument = oldSolution.GetDocument(changedDocumentId);
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
                        var originalDocument = newSolution.GetDocument(changedDocumentId);
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
