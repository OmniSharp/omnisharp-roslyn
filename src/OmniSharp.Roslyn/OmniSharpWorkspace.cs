using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Composition;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Text;
using Microsoft.Extensions.Logging;
using OmniSharp.FileWatching;
using OmniSharp.Roslyn;
using OmniSharp.Roslyn.Utilities;
using OmniSharp.Utilities;

namespace OmniSharp
{
    [Export, Shared]
    public class OmniSharpWorkspace : Workspace
    {
        public bool Initialized { get; set; }
        public BufferManager BufferManager { get; private set; }

        private readonly ILogger<OmniSharpWorkspace> _logger;

        private readonly ConcurrentDictionary<string, ProjectInfo> miscDocumentsProjectInfos = new ConcurrentDictionary<string, ProjectInfo>();

        [ImportingConstructor]
        public OmniSharpWorkspace(HostServicesAggregator aggregator, ILoggerFactory loggerFactory, IFileSystemWatcher fileSystemWatcher)
            : base(aggregator.CreateHostServices(), "Custom")
        {
            BufferManager = new BufferManager(this, fileSystemWatcher);
            _logger = loggerFactory.CreateLogger<OmniSharpWorkspace>();
        }

        public override bool CanOpenDocuments => true;

        public override void OpenDocument(DocumentId documentId, bool activate = true)
        {
            var doc = this.CurrentSolution.GetDocument(documentId);
            if (doc != null)
            {
                var text = doc.GetTextAsync(CancellationToken.None).WaitAndGetResult(CancellationToken.None);
                this.OnDocumentOpened(documentId, text.Container, activate);
            }
        }

        public override void CloseDocument(DocumentId documentId)
        {
            var doc = this.CurrentSolution.GetDocument(documentId);
            if (doc != null)
            {
                var text = doc.GetTextAsync(CancellationToken.None).WaitAndGetResult(CancellationToken.None);
                var version = doc.GetTextVersionAsync(CancellationToken.None).WaitAndGetResult(CancellationToken.None);
                var loader = TextLoader.From(TextAndVersion.Create(text, version, doc.FilePath));
                this.OnDocumentClosed(documentId, loader);
            }
        }

        public void AddProject(ProjectInfo projectInfo)
        {
            OnProjectAdded(projectInfo);
        }

        public void AddProjectReference(ProjectId projectId, ProjectReference projectReference)
        {
            OnProjectReferenceAdded(projectId, projectReference);
        }

        public void RemoveProjectReference(ProjectId projectId, ProjectReference projectReference)
        {
            OnProjectReferenceRemoved(projectId, projectReference);
        }

        public void AddMetadataReference(ProjectId projectId, MetadataReference metadataReference)
        {
            OnMetadataReferenceAdded(projectId, metadataReference);
        }

        public void RemoveMetadataReference(ProjectId projectId, MetadataReference metadataReference)
        {
            OnMetadataReferenceRemoved(projectId, metadataReference);
        }

        public void AddDocument(DocumentInfo documentInfo)
        {
            // if the file has already been added as a misc file,
            // because of a possible race condition between the updates of the project systems,
            // remove the misc file and add the document as required
            TryRemoveMiscellaneousDocument(documentInfo.FilePath);

            OnDocumentAdded(documentInfo);
        }

        public DocumentId TryAddMiscellaneousDocument(string filePath, string language)
        {
            if (GetDocument(filePath) != null)
                return null; //if the workspace already knows about this document then it is not a miscellaneous document

            var projectInfo = miscDocumentsProjectInfos.GetOrAdd(language, (lang) => CreateMiscFilesProject(lang));
            var documentId = AddDocument(projectInfo.Id, filePath);
            _logger.LogInformation($"Miscellaneous file: {filePath} added to workspace");
            return documentId;
        }

        public bool TryRemoveMiscellaneousDocument(string filePath)
        {
            var documentId = GetDocumentId(filePath);
            if (documentId == null || !IsMiscellaneousDocument(documentId))
                return false;

            RemoveDocument(documentId);
            _logger.LogDebug($"Miscellaneous file: {filePath} removed from workspace");
            return true;
        }

        public void TryPromoteMiscellaneousDocumentsToProject(Project project)
        {
            if (project == null)
            {
                throw new ArgumentNullException(nameof(project));
            }

            var miscProjectInfos = miscDocumentsProjectInfos.Values.ToArray();
            for (var i = 0; i < miscProjectInfos.Length; i++)
            {
                var miscProject = CurrentSolution.GetProject(miscProjectInfos[i].Id);
                var documents = miscProject.Documents.ToArray();

                for (var j = 0; j < documents.Length; j++)
                {
                    var document = documents[j];
                    if (FileBelongsToProject(document.FilePath, project))
                    {
                        var textLoader = new DelegatingTextLoader(document);
                        var documentId = DocumentId.CreateNewId(project.Id);
                        var documentInfo = DocumentInfo.Create(
                            documentId,
                            document.FilePath,
                            filePath: document.FilePath,
                            loader: textLoader);

                        // This transitively will remove the document from the misc project.
                        AddDocument(documentInfo);
                    }
                }
            }
        }

        private ProjectInfo CreateMiscFilesProject(string language)
        {
            string assemblyName = Guid.NewGuid().ToString("N");
            var projectInfo = ProjectInfo.Create(
                   id: ProjectId.CreateNewId(),
                   version: VersionStamp.Create(),
                   name: "MiscellaneousFiles.csproj",
                   metadataReferences: DefaultMetadataReferenceHelper.GetDefaultMetadataReferenceLocations()
                                       .Select(loc => MetadataReference.CreateFromFile(loc)),
                   assemblyName: assemblyName,
                   language: language);

            AddProject(projectInfo);
            return projectInfo;
        }

        public DocumentId AddDocument(ProjectId projectId, string filePath, SourceCodeKind sourceCodeKind = SourceCodeKind.Regular)
        {
            var documentId = DocumentId.CreateNewId(projectId);
            this.AddDocument(documentId, projectId, filePath, sourceCodeKind);
            return documentId;
        }

        public DocumentId AddDocument(DocumentId documentId, ProjectId projectId, string filePath, SourceCodeKind sourceCodeKind = SourceCodeKind.Regular)
        {
            var loader = new OmniSharpTextLoader(filePath);
            var documentInfo = DocumentInfo.Create(documentId, filePath, filePath: filePath, loader: loader, sourceCodeKind: sourceCodeKind);

            this.AddDocument(documentInfo);

            return documentId;
        }

        public void RemoveDocument(DocumentId documentId)
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

        public DocumentId GetDocumentId(string filePath)
        {
            var documentIds = CurrentSolution.GetDocumentIdsWithFilePath(filePath);
            return documentIds.FirstOrDefault();
        }

        public IEnumerable<Document> GetDocuments(string filePath)
        {
            return CurrentSolution
                .GetDocumentIdsWithFilePath(filePath)
                .Select(id => CurrentSolution.GetDocument(id));
        }

        public Document GetDocument(string filePath)
        {
            if (string.IsNullOrWhiteSpace(filePath)) return null;

            var documentId = GetDocumentId(filePath);
            if (documentId == null)
            {
                return null;
            }

            return CurrentSolution.GetDocument(documentId);
        }

        public override bool CanApplyChange(ApplyChangesKind feature)
        {
            return true;
        }

        internal bool FileBelongsToProject(string fileName, Project project)
        {
            if (string.IsNullOrWhiteSpace(project.FilePath) ||
                string.IsNullOrWhiteSpace(fileName))
            {
                return false;
            }

            var fileDirectory = new FileInfo(fileName).Directory;
            var projectPath = project.FilePath;
            var projectDirectory = new FileInfo(projectPath).Directory.FullName;

            while (fileDirectory != null)
            {
                if (string.Equals(fileDirectory.FullName, projectDirectory, StringComparison.OrdinalIgnoreCase))
                {
                    return true;
                }

                fileDirectory = fileDirectory.Parent;
            }

            return false;
        }

        protected override void ApplyDocumentRemoved(DocumentId documentId)
        {
            var document = this.CurrentSolution.GetDocument(documentId);
            if (document != null)
            {
                DeleteDocumentFile(document.Id, document.FilePath);
                this.OnDocumentRemoved(documentId);
            }
        }

        private void DeleteDocumentFile(DocumentId id, string fullPath)
        {
            try
            {
                File.Delete(fullPath);
            }
            catch (IOException e)
            {
                LogDeletionException(e, fullPath);
            }
            catch (NotSupportedException e)
            {
                LogDeletionException(e, fullPath);
            }
            catch (UnauthorizedAccessException e)
            {
                LogDeletionException(e, fullPath);
            }
        }

        private void LogDeletionException(Exception e, string filePath)
        {
            _logger.LogError(e, $"Error deleting file {filePath}");
        }

        protected override void ApplyDocumentAdded(DocumentInfo info, SourceText text)
        {
            var project = this.CurrentSolution.GetProject(info.Id.ProjectId);
            var fullPath = info.FilePath;

            this.OnDocumentAdded(info);

            if (text != null)
            {
                this.SaveDocumentText(info.Id, fullPath, text, text.Encoding ?? Encoding.UTF8);
            }
        }

        private void SaveDocumentText(DocumentId id, string fullPath, SourceText newText, Encoding encoding)
        {
            try
            {
                var dir = Path.GetDirectoryName(fullPath);
                if (!Directory.Exists(dir))
                {
                    Directory.CreateDirectory(dir);
                }

                using (var writer = new StreamWriter(fullPath, append: false, encoding: encoding))
                {
                    newText.Write(writer);
                }
            }
            catch (IOException e)
            {
                _logger.LogError(e, $"Error saving document {fullPath}");
            }
        }

        public bool IsCapableOfSemanticDiagnostics(Document document)
        {
            return !IsMiscellaneousDocument(document.Id);
        }

        private bool IsMiscellaneousDocument(DocumentId documentId)
        {
            return miscDocumentsProjectInfos.Where(p => p.Value.Id == documentId.ProjectId).Any();
        }

        private class DelegatingTextLoader : TextLoader
        {
            private readonly Document _fromDocument;

            public DelegatingTextLoader(Document fromDocument)
            {
                _fromDocument = fromDocument ?? throw new ArgumentNullException(nameof(fromDocument));
            }

            public override async Task<TextAndVersion> LoadTextAndVersionAsync(
                Workspace workspace,
                DocumentId documentId,
                CancellationToken cancellationToken)
            {
                var sourceText = await _fromDocument.GetTextAsync();
                var version = await _fromDocument.GetTextVersionAsync();
                var textAndVersion = TextAndVersion.Create(sourceText, version);

                return textAndVersion;
            }
        }
    }
}
