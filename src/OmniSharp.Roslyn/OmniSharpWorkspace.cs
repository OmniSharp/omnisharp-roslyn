using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Composition;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Diagnostics;
using Microsoft.CodeAnalysis.Host;
using Microsoft.CodeAnalysis.Text;
using Microsoft.Extensions.Logging;
using OmniSharp.FileSystem;
using OmniSharp.FileWatching;
using OmniSharp.Roslyn;
using OmniSharp.Roslyn.EditorConfig;
using OmniSharp.Roslyn.Utilities;
using OmniSharp.Utilities;

namespace OmniSharp
{
    [Export, Shared]
    public class OmniSharpWorkspace : Workspace
    {
        public bool Initialized
        {
            get { return isInitialized; }
            set
            {
                if (isInitialized == value) return;
                isInitialized = value;
                OnInitialized(isInitialized);
            }
        }

        public event Action<bool> OnInitialized = delegate { };

        public bool EditorConfigEnabled { get; set; }
        public BufferManager BufferManager { get; private set; }

        private readonly ILogger<OmniSharpWorkspace> _logger;

        private readonly ConcurrentBag<Func<string, Task>> _waitForProjectModelReadyHandlers = new ConcurrentBag<Func<string, Task>>();
        private readonly ConcurrentDictionary<string, ProjectInfo> miscDocumentsProjectInfos = new ConcurrentDictionary<string, ProjectInfo>();
        private readonly ConcurrentDictionary<ProjectId, Predicate<string>> documentInclusionRulesPerProject = new ConcurrentDictionary<ProjectId, Predicate<string>>();
        private bool isInitialized;

        [ImportingConstructor]
        public OmniSharpWorkspace(HostServicesAggregator aggregator, ILoggerFactory loggerFactory, IFileSystemWatcher fileSystemWatcher)
            : this(aggregator.CreateHostServices(), loggerFactory, fileSystemWatcher)
        {
        }
        
        public OmniSharpWorkspace(HostServices hostServices, ILoggerFactory loggerFactory, IFileSystemWatcher fileSystemWatcher)
            : base(hostServices, "Custom")
        {
            BufferManager = new BufferManager(this, loggerFactory, fileSystemWatcher);
            _logger = loggerFactory.CreateLogger<OmniSharpWorkspace>();
            fileSystemWatcher.WatchDirectories(OnDirectoryRemoved);
        }

        public override bool CanOpenDocuments => true;


        private void OnDirectoryRemoved(string path, FileChangeType changeType)
        {
            if (changeType == FileChangeType.DirectoryDelete)
            {
                var docs = CurrentSolution.Projects.SelectMany(x => x.Documents)
                    .Where(x => x.FilePath.StartsWith(path + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase));

                foreach (var doc in docs)
                {
                    OnDocumentRemoved(doc.Id);
                }
            }
        }

        public void AddWaitForProjectModelReadyHandler(Func<string, Task> handler)
        {
            _waitForProjectModelReadyHandlers.Add(handler);
        }

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

        public void AddDocumentInclusionRuleForProject(ProjectId projectId, Predicate<string> documentPathFilter)
        {
            documentInclusionRulesPerProject[projectId] = documentPathFilter;
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

        public DocumentId TryAddMiscellaneousDocument(string filePath, TextLoader loader, string language)
        {
            if (GetDocument(filePath) != null)
                return null; //if the workspace already knows about this document then it is not a miscellaneous document

            var projectInfo = miscDocumentsProjectInfos.GetOrAdd(language, (lang) => CreateMiscFilesProject(lang));
            var documentId = AddDocument(projectInfo.Id, filePath, loader);
            _logger.LogInformation($"Miscellaneous file: {filePath} added to workspace");

            if (!EditorConfigEnabled)
            {
                return documentId;
            }

            var analyzerConfigFiles = projectInfo.AnalyzerConfigDocuments.Select(document => document.FilePath);
            var newAnalyzerConfigFiles = EditorConfigFinder
                .GetEditorConfigPaths(filePath)
                .Except(analyzerConfigFiles);

            foreach (var analyzerConfigFile in newAnalyzerConfigFiles)
            {
                AddAnalyzerConfigDocument(projectInfo.Id, analyzerConfigFile);
            }

            return documentId;
        }

        public DocumentId TryAddMiscellaneousDocument(string filePath, string language)
        {
            return TryAddMiscellaneousDocument(filePath, new OmniSharpTextLoader(filePath), language);
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

        public void UpdateDiagnosticOptionsForProject(ProjectId projectId, ImmutableDictionary<string, ReportDiagnostic> rules)
        {
            var project = this.CurrentSolution.GetProject(projectId);
            OnCompilationOptionsChanged(projectId, project.CompilationOptions.WithSpecificDiagnosticOptions(rules));
        }

        public void UpdateCompilationOptionsForProject(ProjectId projectId, CompilationOptions options)
        {
            OnCompilationOptionsChanged(projectId, options);
        }

        private ProjectInfo CreateMiscFilesProject(string language)
        {
            var projectInfo = ProjectInfo.Create(
                   id: ProjectId.CreateNewId(),
                   version: VersionStamp.Create(),
                   name: $"{Configuration.OmniSharpMiscProjectName}.csproj",
                   metadataReferences: DefaultMetadataReferenceHelper.GetDefaultMetadataReferenceLocations()
                                       .Select(loc => MetadataReference.CreateFromFile(loc)),
                   assemblyName: Configuration.OmniSharpMiscProjectName,
                   language: language);

            AddProject(projectInfo);
            return projectInfo;
        }

        public void AddDocument(DocumentInfo documentInfo)
        {
            // if the file has already been added as a misc file,
            // because of a possible race condition between the updates of the project systems,
            // remove the misc file and add the document as required
            TryRemoveMiscellaneousDocument(documentInfo.FilePath);

            OnDocumentAdded(documentInfo);
        }

        public DocumentId AddDocument(ProjectId projectId, string filePath, SourceCodeKind sourceCodeKind = SourceCodeKind.Regular)
        {
            var project = this.CurrentSolution.GetProject(projectId);
            return AddDocument(project, filePath, sourceCodeKind);
        }

        public DocumentId AddDocument(ProjectId projectId, string filePath, TextLoader loader, SourceCodeKind sourceCodeKind = SourceCodeKind.Regular)
        {
            var documentId = DocumentId.CreateNewId(projectId);
            var project = this.CurrentSolution.GetProject(projectId);
            return AddDocument(documentId, project, filePath, loader, sourceCodeKind);
        }

        public DocumentId AddDocument(Project project, string filePath, SourceCodeKind sourceCodeKind = SourceCodeKind.Regular)
        {
            var documentId = DocumentId.CreateNewId(project.Id);
            return AddDocument(documentId, project, filePath, sourceCodeKind);
        }

        public DocumentId AddDocument(DocumentId documentId, Project project, string filePath, SourceCodeKind sourceCodeKind = SourceCodeKind.Regular)
        {
            return AddDocument(documentId, project, filePath, new OmniSharpTextLoader(filePath), sourceCodeKind);
        }

        internal DocumentId AddDocument(DocumentId documentId, ProjectId projectId, string filePath, TextLoader loader, SourceCodeKind sourceCodeKind = SourceCodeKind.Regular)
        {
            var project = this.CurrentSolution.GetProject(projectId);
            return AddDocument(documentId, project, filePath, loader, sourceCodeKind);
        }

        internal DocumentId AddDocument(DocumentId documentId, Project project, string filePath, TextLoader loader, SourceCodeKind sourceCodeKind = SourceCodeKind.Regular)
        {
            var basePath = Path.GetDirectoryName(project.FilePath);
            var fullPath = Path.GetDirectoryName(filePath);

            IEnumerable<string> folders = null;

            // folder computation is best effort. in case of exceptions, we back out because it's not essential for core features
            try
            {
                // find the relative path from project file to our document
                var relativeDocumentPath = FileSystemHelper.GetRelativePath(fullPath, basePath);

                // only set document's folders if
                // 1. relative path was computed
                // 2. path is not pointing any level up
                if (relativeDocumentPath != null && !relativeDocumentPath.StartsWith(".."))
                {
                    folders = relativeDocumentPath?.Split(new[] { Path.DirectorySeparatorChar });
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, $"An error occurred when computing a relative path from {basePath} to {fullPath}. Document at {filePath} will be processed without folder structure.");
            }

            var documentInfo = DocumentInfo.Create(documentId, Path.GetFileName(filePath), folders: folders, filePath: filePath, loader: loader, sourceCodeKind: sourceCodeKind);
            AddDocument(documentInfo);

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
                .Select(id => CurrentSolution.GetDocument(id))
                .OfType<Document>();
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

        public async Task<IEnumerable<Document>> GetDocumentsFromFullProjectModelAsync(string filePath)
        {
            await OnWaitForProjectModelReadyAsync(filePath);
            return GetDocuments(filePath);
        }

        public async Task<Document> GetDocumentFromFullProjectModelAsync(string filePath)
        {
            await OnWaitForProjectModelReadyAsync(filePath);
            return GetDocument(filePath);
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

            // File path needs to be checked against any rules defined by the specific project system. (e.g. MSBuild default excluded folders)
            if (documentInclusionRulesPerProject.TryGetValue(project.Id, out Predicate<string> documentInclusionFilter))
            {
                return documentInclusionFilter(fileName);
            }

            // if no custom rule set for this ProjectId, fallback to simple directory heuristic.
            var fileDirectory = new FileInfo(fileName).Directory;
            var projectPath = project.FilePath;
            var projectDirectory = new FileInfo(projectPath).Directory.FullName;
            var otherProjectDirectories = CurrentSolution.Projects
                .Where(p => p != project && !string.IsNullOrWhiteSpace(p.FilePath))
                .Select(p => new FileInfo(p.FilePath).Directory.FullName)
                .ToImmutableArray();

            while (fileDirectory != null)
            {
                if (string.Equals(fileDirectory.FullName, projectDirectory, StringComparison.OrdinalIgnoreCase))
                {
                    return true;
                }

                // if any project is closer to the file, file should belong to that project.
                if (otherProjectDirectories.Contains(fileDirectory.FullName, StringComparer.OrdinalIgnoreCase))
                {
                    return false;
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
                if (!string.IsNullOrEmpty(dir) && !Directory.Exists(dir))
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

            public override async Task<TextAndVersion> LoadTextAndVersionAsync(LoadTextOptions options, CancellationToken cancellationToken)
            {
                var sourceText = await _fromDocument.GetTextAsync();
                var version = await _fromDocument.GetTextVersionAsync();
                var textAndVersion = TextAndVersion.Create(sourceText, version);

                return textAndVersion;
            }
        }

        private Task OnWaitForProjectModelReadyAsync(string filePath)
        {
            return Task.WhenAll(_waitForProjectModelReadyHandlers.Select(h => h(filePath)));
        }

        public void SetAnalyzerReferences(ProjectId id, ImmutableArray<AnalyzerFileReference> analyzerReferences)
        {
            var project = this.CurrentSolution.GetProject(id);

            var refsToAdd = analyzerReferences.Where(newRef => project.AnalyzerReferences.All(oldRef => oldRef.Display != newRef.Display));
            var refsToRemove = project.AnalyzerReferences.Where(newRef => analyzerReferences.All(oldRef => oldRef.Display != newRef.Display));

            foreach (var toAdd in refsToAdd)
            {
                _logger.LogInformation($"Adding analyzer reference: {toAdd.FullPath}");
                base.OnAnalyzerReferenceAdded(id, toAdd);
            }

            foreach (var toRemove in refsToRemove)
            {
                _logger.LogInformation($"Removing analyzer reference: {toRemove.FullPath}");
                base.OnAnalyzerReferenceRemoved(id, toRemove);
            }
        }

        public void AddAdditionalDocument(ProjectId projectId, string filePath)
        {
            var loader = new OmniSharpTextLoader(filePath);
            AddAdditionalDocument(projectId, filePath, loader);
        }

        public void AddAdditionalDocument(ProjectId projectId, string filePath, TextLoader loader)
        {
            var documentId = DocumentId.CreateNewId(projectId);
            var documentInfo = DocumentInfo.Create(documentId, Path.GetFileName(filePath), filePath: filePath, loader: loader);
            OnAdditionalDocumentAdded(documentInfo);
        }

        public void AddAnalyzerConfigDocument(ProjectId projectId, string filePath)
        {
            var documentId = DocumentId.CreateNewId(projectId);
            var loader = new OmniSharpTextLoader(filePath);
            var documentInfo = DocumentInfo.Create(documentId, Path.GetFileName(filePath), filePath: filePath, loader: loader);
            OnAnalyzerConfigDocumentAdded(documentInfo);
        }

        public void ReloadAnalyzerConfigDocument(DocumentId documentId, string filePath)
        {
            var loader = new OmniSharpTextLoader(filePath);
            OnAnalyzerConfigDocumentTextLoaderChanged(documentId, loader);
        }

        public void RemoveAdditionalDocument(DocumentId documentId)
        {
            OnAdditionalDocumentRemoved(documentId);
        }

        public void RemoveAnalyzerConfigDocument(DocumentId documentId)
        {
            OnAnalyzerConfigDocumentRemoved(documentId);
        }

        protected override void ApplyProjectChanges(ProjectChanges projectChanges)
        {
            // since Roslyn currently doesn't handle DefaultNamespace changes via ApplyProjectChanges
            // and OnDefaultNamespaceChanged is internal, we use reflection for now
            if (projectChanges.NewProject.DefaultNamespace != projectChanges.OldProject.DefaultNamespace)
            {
                var onDefaultNamespaceChanged = this.GetType().GetMethod("OnDefaultNamespaceChanged", BindingFlags.Instance | BindingFlags.NonPublic);
                if (onDefaultNamespaceChanged != null)
                {
                    onDefaultNamespaceChanged.Invoke(this, new object[] { projectChanges.ProjectId, projectChanges.NewProject.DefaultNamespace });
                }
            }

            base.ApplyProjectChanges(projectChanges);
        }
    }
}
