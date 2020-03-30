using Microsoft.CodeAnalysis;
using OmniSharp.Extensions;
using OmniSharp.Services;
using OmniSharp.Utilities;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace OmniSharp.Roslyn.CSharp.Services.Decompilation
{
    // due to dependency on Microsoft.CodeAnalysis.Editor.CSharp
    // this class supports only net472
    public class DecompilationExternalSourceService : BaseExternalSourceService, IExternalSourceService
    {
        private const string CSharpDecompiledSourceService = "Microsoft.CodeAnalysis.Editor.CSharp.DecompiledSource.CSharpDecompiledSourceService";
        private const string DecompiledKey = "$Decompiled$";
        private readonly Lazy<Assembly> _editorFeaturesAssembly;
        private readonly Lazy<Type> _csharpDecompiledSourceService;

        public DecompilationExternalSourceService(IAssemblyLoader loader) : base(loader)
        {
            _editorFeaturesAssembly = _loader.LazyLoad(Configuration.RoslynEditorFeatures);
            _csharpDecompiledSourceService = _editorFeaturesAssembly.LazyGetType(CSharpDecompiledSourceService);
        }

        public async Task<(Document metadataDocument, string documentPath)> GetAndAddExternalSymbolDocument(Project project, ISymbol symbol, CancellationToken cancellationToken)
        {
            var fileName = symbol.GetFilePathForExternalSymbol(project);

            Project decompilationProject;

            // since submission projects cannot have new documents added to it
            // we will use a separate project to hold decompiled documents
            if (project.IsSubmission)
            {
                decompilationProject = project.Solution.Projects.FirstOrDefault(x => x.Name == DecompiledKey);
                if (decompilationProject == null)
                {
                    decompilationProject = project.Solution.AddProject(DecompiledKey, $"{DecompiledKey}.dll", LanguageNames.CSharp)
                        .WithCompilationOptions(project.CompilationOptions)
                        .WithMetadataReferences(project.MetadataReferences);
                }
            }
            else
            {
                // for regular projects we will use current project to store decompiled docs
                decompilationProject = project;
            }

            if (!_cache.TryGetValue(fileName, out var metadataDocument))
            {
                var topLevelSymbol = symbol.GetTopLevelContainingNamedType();

                var temporaryDocument = decompilationProject.AddDocument(fileName, string.Empty);
                var method = _csharpDecompiledSourceService.GetMethod(AddSourceToAsync);

                var service = Activator.CreateInstance(_csharpDecompiledSourceService.Value, new object[] { temporaryDocument.Project.LanguageServices });
                var documentTask = method.Invoke<Task<Document>>(service, new object[] { temporaryDocument, await decompilationProject.GetCompilationAsync(), topLevelSymbol, cancellationToken });
                metadataDocument = await documentTask;

                _cache[fileName] = metadataDocument;
            }

            return (metadataDocument, fileName);
        }
    }
}
