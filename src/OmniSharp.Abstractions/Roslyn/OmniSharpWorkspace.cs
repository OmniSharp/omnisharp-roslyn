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
using Microsoft.Framework.Logging;
using Microsoft.Framework.Runtime;
using OmniSharp.Mef;
using OmniSharp.Roslyn;
using OmniSharp.Services;
using OmniSharp.Stdio.Services;

namespace OmniSharp
{
    class GenericProvider<T> : ExportDescriptorProvider
    {
        private readonly T _item;

        public GenericProvider(T item)
        {
            _item = item;
        }

        public override IEnumerable<ExportDescriptorPromise> GetExportDescriptors(CompositionContract contract, DependencyAccessor descriptorAccessor)
        {
            if (contract.ContractType == typeof(T))
            {
                yield return new ExportDescriptorPromise(contract, string.Empty, true,
                    () => Enumerable.Empty<CompositionDependency>(),
                    deps => ExportDescriptor.Create((context, operation) => _item, new Dictionary<string, object>()));
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

        public void ConfigurePluginHost(IServiceProvider serviceProvider,
                              ILoggerFactory loggerFactory,
                              IOmnisharpEnvironment env,
                              ISharedTextWriter writer,
                              IEnumerable<Assembly> assemblies)
        {
            Instance = this;

            var config = new ContainerConfiguration();
            foreach (var assembly in assemblies)
            {
                config = config.WithAssembly(assembly);
            }

            //IOmnisharpEnvironment env, ILoggerFactory loggerFactory
            config = config.WithProvider(new GenericProvider<OmnisharpWorkspace>(this));
            config = config.WithProvider(new GenericProvider<IServiceProvider>(serviceProvider));
            config = config.WithProvider(new GenericProvider<ILoggerFactory>(loggerFactory));
            config = config.WithProvider(new GenericProvider<IOmnisharpEnvironment>(env));
            config = config.WithProvider(new GenericProvider<ISharedTextWriter>(writer));

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
