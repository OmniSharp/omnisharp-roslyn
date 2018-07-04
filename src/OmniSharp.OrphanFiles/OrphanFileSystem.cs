using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Composition;
using System.IO;
using System.Threading.Tasks;
using Microsoft.CodeAnalysis;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using OmniSharp.FileSystem;
using OmniSharp.FileWatching;
using OmniSharp.Models.WorkspaceInformation;
using OmniSharp.MSBuild;
using OmniSharp.Roslyn;
using OmniSharp.Services;

namespace OmniSharp.OrphanFiles
{
    [Export(typeof(IProjectSystem)), Shared]
    public class OrphanFileSystem : IProjectSystem
    {
        public string Key { get; } = "OrphanFiles";
        public string Language { get; } = LanguageNames.CSharp;
        IEnumerable<string> IProjectSystem.Extensions => throw new NotImplementedException();
        public bool EnabledByDefault { get; } = true;

        private readonly ConcurrentDictionary<string, ProjectInfo> _projects = new ConcurrentDictionary<string, ProjectInfo>();
        private readonly OmniSharpWorkspace _workspace;
        private readonly IFileSystemWatcher _fileSystemWatcher;
        private readonly FileSystemHelper _fileSystemHelper;
        private readonly IProjectSystem _projectSystem;
        private readonly ILogger _logger;

        [ImportingConstructor]
        public OrphanFileSystem(OmniSharpWorkspace workspace, IFileSystemWatcher fileSystemWatcher, FileSystemHelper fileSystemHelper, ILoggerFactory loggerFactory, [Import] ProjectSystem projectSystem)
        {
            _workspace = workspace;
            _fileSystemWatcher = fileSystemWatcher ?? throw new ArgumentNullException(nameof(fileSystemWatcher));
            _fileSystemHelper = fileSystemHelper;
            _logger = loggerFactory.CreateLogger<OrphanFileSystem>();
            _projectSystem = projectSystem;
        }

        Task<object> IProjectSystem.GetProjectModelAsync(string filePath)
        {
            return Task.FromResult<object>(null);
        }

        Task<object> IProjectSystem.GetWorkspaceModelAsync(WorkspaceInformationRequest request)
        {
            return Task.FromResult<object>(null);
        }

        void IProjectSystem.Initalize(IConfiguration configuration)
        {
            var allFiles = _fileSystemHelper.GetFiles("**/*.cs");
            var solution = _workspace.CurrentSolution;

            foreach (var file in allFiles)
            {
                AddFileToWorkSpace(file);
            }
        }

        private async void AddFileToWorkSpace(string filename)
        {
            string assemblyName = Guid.NewGuid().ToString("N");
            await _projectSystem.GetProjectModelAsync(filename);
            var document = _workspace.GetDocument(filename);
            if (document == null)
            {
                var newProject = ProjectInfo.Create(
                   filePath: filename,
                   id: ProjectId.CreateNewId(),
                   version: VersionStamp.Create(),
                   name: Path.GetFileName(filename),
                   metadataReferences: new MetadataReference[] { MetadataReference.CreateFromFile((typeof(object).Assembly).Location) },
                   assemblyName: assemblyName,
                   //TODO: Ask if there should be other languages as well 
                   language: Language);

                _workspace.AddProject(newProject);
                _workspace.AddDocument(newProject.Id, filename);

                _logger.LogInformation($"Successfully added file '{filename}' to workspace");
            }
        }
    }
}
