using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Composition;
using System.IO;
using System.Threading.Tasks;
using Microsoft.CodeAnalysis;
using Microsoft.Extensions.Configuration;
using OmniSharp.FileSystem;
using OmniSharp.FileWatching;
using OmniSharp.Models.WorkspaceInformation;
using OmniSharp.Services;

namespace OmniSharp.OrphanFiles
{
    [Export(typeof(IProjectSystem)), Shared]
    public class OrphanFileSystem : IProjectSystem
    {
        public string Key { get; } = "MsBuild";
        public string Language { get; } = LanguageNames.CSharp;
        IEnumerable<string> IProjectSystem.Extensions => throw new NotImplementedException();
        public bool EnabledByDefault { get; } = true;

        private readonly ConcurrentDictionary<string, ProjectInfo> _projects = new ConcurrentDictionary<string, ProjectInfo>();
        private readonly OmniSharpWorkspace _workspace;
        private readonly IFileSystemWatcher _fileSystemWatcher;
        private readonly FileSystemHelper _fileSystemHelper;

        [ImportingConstructor]
        public OrphanFileSystem(OmniSharpWorkspace workspace, IFileSystemWatcher fileSystemWatcher, FileSystemHelper fileSystemHelper)
        {
            _workspace = workspace;
            _fileSystemWatcher = fileSystemWatcher ?? throw new ArgumentNullException(nameof(fileSystemWatcher));
            _fileSystemHelper = fileSystemHelper;
        }

        Task<object> IProjectSystem.GetProjectModelAsync(string filePath)
        {
            throw new NotImplementedException();
        }

        Task<object> IProjectSystem.GetWorkspaceModelAsync(WorkspaceInformationRequest request)
        {
            throw new NotImplementedException();
        }

        void IProjectSystem.Initalize(IConfiguration configuration)
        {
            var allFiles = _fileSystemHelper.GetFiles("**/*.cs");
            var solution = _workspace.CurrentSolution;
            string assemblyName = Guid.NewGuid().ToString("N");

            foreach (var file in allFiles)
            {
                if (_workspace.GetDocument(file) == null)
                {
                    var doc = _workspace.GetDocument(file);
                    //implies the document doesnot exist in the workspace currently
                    var project = ProjectInfo.Create(
                        filePath: file,
                        id: ProjectId.CreateNewId(),
                        version: VersionStamp.Create(),
                        name: Path.GetFileName(file),
                        metadataReferences: new MetadataReference[] { MetadataReference.CreateFromFile((typeof(object).Assembly).Location) },
                        assemblyName: assemblyName,
                        //TODO: Ask if there should be other languages as well 
                        language: Language);

                    _workspace.AddProject(project);
                    _workspace.AddDocument(project.Id, file);
                }
            }
        }
    }
}
