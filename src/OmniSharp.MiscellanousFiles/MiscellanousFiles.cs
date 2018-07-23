using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.ComponentModel;
using System.Composition;
using System.IO;
using System.Threading.Tasks;
using Microsoft.CodeAnalysis;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using OmniSharp.FileSystem;
using OmniSharp.FileWatching;
using OmniSharp.Mef;
using OmniSharp.Models.WorkspaceInformation;
using OmniSharp.MSBuild;
using OmniSharp.Services;

namespace OmniSharp.MiscellanousFiles
{
    [ExtensionOrder(After = nameof(ProjectSystem))]
    [ExportIProjectSystem(nameof(MiscellanousFiles)), Shared]
    public class MiscellanousFiles : IProjectSystem
    {
        private readonly string miscFileExtension = ".cs";
        public string Key { get; } = "MiscellanousFiles";
        public string Language { get; } = LanguageNames.CSharp;
        IEnumerable<string> IProjectSystem.Extensions => new[] { miscFileExtension };
        public bool EnabledByDefault { get; } = true;

        private readonly ConcurrentDictionary<string, ProjectInfo> _projects = new ConcurrentDictionary<string, ProjectInfo>();
        private readonly OmniSharpWorkspace _workspace;
        private readonly IFileSystemWatcher _fileSystemWatcher;
        private readonly FileSystemHelper _fileSystemHelper;
        private readonly ProjectSystem _projectSystem;
        private readonly ILogger _logger;

        [ImportingConstructor]
        public MiscellanousFiles(OmniSharpWorkspace workspace, IFileSystemWatcher fileSystemWatcher, FileSystemHelper fileSystemHelper, ILoggerFactory loggerFactory, [Import] ProjectSystem projectSystem)
        {
            _workspace = workspace;
            _fileSystemWatcher = fileSystemWatcher ?? throw new ArgumentNullException(nameof(fileSystemWatcher));
            _fileSystemHelper = fileSystemHelper;
            _logger = loggerFactory.CreateLogger<MiscellanousFiles>();
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
            foreach (var filePath in allFiles)
                AddIfMiscellanousFile(filePath);

            _fileSystemWatcher.Watch(miscFileExtension, OnMiscellanousFileChanged);
        }

        private async void AddIfMiscellanousFile(string filePath)
        {
            //wait for the project system to get initialised
            await _projectSystem.HasCompletedUpdateRequest();
            if (_workspace.GetDocument(filePath) == null)
            {
                string assemblyName = Guid.NewGuid().ToString("N");
                var projectInfo = ProjectInfo.Create(
                   filePath: filePath,
                   id: ProjectId.CreateNewId(),
                   version: VersionStamp.Create(),
                   name: Path.GetFileName(filePath),
                   metadataReferences: new MetadataReference[] { MetadataReference.CreateFromFile((typeof(object).Assembly).Location) },
                   assemblyName: assemblyName,
                   language: Language);

                _workspace.AddProject(projectInfo);
                _workspace.AddMiscellanousFileDocument(projectInfo.Id, filePath);
                _projects[filePath] = projectInfo;
                _logger.LogInformation($"Successfully added file '{filePath}' to workspace");
            }
        }

        private void OnMiscellanousFileChanged(string filePath, FileChangeType changeType)
        {
            if (changeType == FileChangeType.Unspecified && File.Exists(filePath) ||
                changeType == FileChangeType.Create)
            {
                AddIfMiscellanousFile(filePath);
            }

            else if(changeType == FileChangeType.Unspecified && !File.Exists(filePath) ||
                changeType == FileChangeType.Delete)
            {
                RemoveFromWorkspace(filePath);       
            }
        }

        private void RemoveFromWorkspace(string filePath)
        {
            if (_projects.TryRemove(filePath, out var project))
            {
                _workspace.RemoveMiscellanousFileDocument(project.Id);
                _logger.LogDebug($"Removed file '{filePath}' from the workspace.");
            }
        }
    }
}
