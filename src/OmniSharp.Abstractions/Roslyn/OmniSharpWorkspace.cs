using System;
using System.Collections.Generic;
using System.Composition;
using System.Composition.Convention;
using System.Composition.Hosting;
using System.Composition.Hosting.Core;
using System.Linq;
using System.Reflection;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Host.Mef;
using Microsoft.CodeAnalysis.Text;
using Microsoft.Framework.Runtime;
using OmniSharp.Mef;
using OmniSharp.Roslyn;

namespace OmniSharp
{
    class OmnisharpWorkspaceProvider : ExportDescriptorProvider
    {
        private readonly OmnisharpWorkspace _workspace;

        public OmnisharpWorkspaceProvider(OmnisharpWorkspace workspace)
        {
            _workspace = workspace;
        }

        public override IEnumerable<ExportDescriptorPromise> GetExportDescriptors(CompositionContract contract, DependencyAccessor descriptorAccessor)
        {
            if (contract.ContractType == typeof(OmnisharpWorkspace))
            {
                yield return new ExportDescriptorPromise(contract, string.Empty, true,
                    () => Enumerable.Empty<CompositionDependency>(),
                    deps => ExportDescriptor.Create((context, operation) => _workspace, new Dictionary<string, object>()));
            }
        }
    }

    public class OmnisharpWorkspace : Workspace
    {
        public bool Initialized { get; set; }

        public BufferManager BufferManager { get; private set; }

        public CompositionHost PluginHost { get; private set; }

        public static OmnisharpWorkspace Instance { get; private set; }

        public OmnisharpWorkspace() : this(MefHostServices.DefaultHost)
        {
        }

        public OmnisharpWorkspace(MefHostServices hostServices) : base(hostServices, "Custom")
        {
            BufferManager = new BufferManager(this);
        }

        public void ConfigurePluginHost(IEnumerable<Assembly> assemblies)
        {
            Instance = this;

            var config = new ContainerConfiguration();
            foreach (var assembly in assemblies)
            {
                config = config.WithAssembly(assembly);
            }

            config = config.WithProvider(new OmnisharpWorkspaceProvider(this));

            var compositionHost = config.CreateContainer();
            PluginHost = compositionHost;
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
            OnDocumentAdded(documentInfo);
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
            return CurrentSolution.GetDocumentIdsWithFilePath(filePath).Select(id => CurrentSolution.GetDocument(id));
        }

        public Document GetDocument(string filePath)
        {
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
    }
}
