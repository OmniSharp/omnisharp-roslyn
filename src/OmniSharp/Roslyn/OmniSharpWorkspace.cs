using System.Collections.Generic;
using System.Composition.Hosting;
using System.Linq;
using System.Reflection;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Host.Mef;
using Microsoft.CodeAnalysis.Text;
using OmniSharp.Models;
using OmniSharp.Roslyn;

namespace OmniSharp
{
    public static class MefContainer
    {
        static CompositionHost container;
        public static CompositionHost Container
        {
            get
            {
                if (container == null)
                {
                    var csharpFeatures = Assembly.Load(new AssemblyName("Microsoft.CodeAnalysis.CSharp.Features, Version=1.0.0.0, Culture=neutral, PublicKeyToken=31bf3856ad364e35"));
                    var features = Assembly.Load(new AssemblyName("Microsoft.CodeAnalysis.Features, Version=1.0.0.0, Culture=neutral, PublicKeyToken=31bf3856ad364e35"));

                    container = new ContainerConfiguration()
                                .WithAssemblies(MefHostServices.DefaultAssemblies.Add(features).Add(csharpFeatures))
                                .CreateContainer();
                }

                return container;
            }
        }
    }

    public class OmnisharpWorkspace : Workspace
    {
        public bool Initialized { get; set; }

        public BufferManager BufferManager { get; private set; }

        public OmnisharpWorkspace() : base(new MefHostServices(MefContainer.Container), "Custom")
        {
            BufferManager = new BufferManager(this);
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
