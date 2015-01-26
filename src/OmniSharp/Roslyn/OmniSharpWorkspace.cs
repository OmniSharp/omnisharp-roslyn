using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Host.Mef;
using Microsoft.CodeAnalysis.Text;
using OmniSharp.Models;

namespace OmniSharp
{
    public class OmnisharpWorkspace : Workspace
    {
        public bool Initialized { get; set; }

        public OmnisharpWorkspace() : base(MefHostServices.DefaultHost, "Custom")
        {
        }

        public void AddProject(ProjectInfo projectInfo)
        {
            OnProjectAdded(projectInfo);
        }

        public new void AddProjectReference(ProjectId projectId, ProjectReference projectReference)
        {
            OnProjectReferenceAdded(projectId, projectReference);
        }

        public new void RemoveProjectReference(ProjectId projectId, ProjectReference projectReference)
        {
            OnProjectReferenceRemoved(projectId, projectReference);
        }

        public new void AddMetadataReference(ProjectId projectId, MetadataReference metadataReference)
        {
            OnMetadataReferenceAdded(projectId, metadataReference);
        }

        public new void RemoveMetadataReference(ProjectId projectId, MetadataReference metadataReference)
        {
            OnMetadataReferenceRemoved(projectId, metadataReference);
        }

        public void AddDocument(DocumentInfo documentInfo)
        {
            OnDocumentAdded(documentInfo);
        }

        public new void RemoveDocument(DocumentId documentId)
        {
            OnDocumentRemoved(documentId);
        }

        public void RemoveProject(ProjectId projectId)
        {
            OnProjectRemoved(projectId);
        }

        public void SetCompilationOptions(ProjectId projectId, CompilationOptions options)
        {
            OnCompilationOptionsChanged(projectId, options);
        }

        public void SetParseOptions(ProjectId projectId, ParseOptions parseOptions)
        {
            OnParseOptionsChanged(projectId, parseOptions);
        }

        public void OnDocumentChanged(DocumentId documentId, SourceText text)
        {
            OnDocumentTextChanged(documentId, text, PreservationMode.PreserveIdentity);
        }

        public void EnsureBufferUpdated(string buffer, string fileName)
        {
            if(buffer == null || fileName == null)
            {
                return;
            }

            var sourceText = SourceText.From(buffer);
            foreach (var documentId in CurrentSolution.GetDocumentIdsWithFilePath(fileName))
            {
                OnDocumentChanged(documentId, sourceText);
            }
        }

        public DocumentId GetDocumentId(string filePath)
        {
            var documentIds = CurrentSolution.GetDocumentIdsWithFilePath(filePath);
            return documentIds.FirstOrDefault();
        }
        
        public IEnumerable<Document> GetDocuments(string filePath)
        {
            return CurrentSolution.GetDocumentIdsWithFilePath(filePath).Select(id => CurrentSolution.GetDocument(id));
        }

        public Document GetDocument(string filePath)
        {
            var documentId = GetDocumentId(filePath);
            if(documentId == null)
            {
                return null;
            }
            return CurrentSolution.GetDocument(documentId);
        }

        protected override void ChangedDocumentText(DocumentId id, SourceText text)
        {
            OnDocumentChanged(id, text);
        }
    }
}
