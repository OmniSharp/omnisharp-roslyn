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
using OmniSharp.Models.WorkspaceInformation;
using OmniSharp.MSBuild;
using OmniSharp.Services;

namespace OmniSharp.MicellanousFiles
{
    [DisplayName(nameof(MiscellanousFiles))]
    [ExtensionOrder(After = nameof(ProjectSystem))]
    [Export(typeof(IProjectSystem)), Shared]
    public class MiscellanousFiles : IProjectSystem
    {
        private readonly string miscFileExtension = ".cs";
        public string Key { get; } = "MiscellanousFiles";
        public string Language { get; } = LanguageNames.CSharp;
        IEnumerable<string> IProjectSystem.Extensions => new[] { miscFileExtension };
        public bool EnabledByDefault { get; } = true;

        private readonly ConcurrentDictionary<string, DocumentId> _documents = new ConcurrentDictionary<string, DocumentId>();
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

            _fileSystemWatcher.Watch(miscFileExtension, onFileChanged);
        }

        private async void AddIfMiscellanousFile(string filePath)
        {
            //wait for the project system to get initialised
            await _projectSystem.hasCompletedUpdateRequest();
            if (_workspace.GetDocument(filePath) == null)
            {
                var documentId =_workspace.AddMiscellanousFile(filePath, Language);
                _documents.TryAdd(filePath, documentId);
                _logger.LogInformation($"Successfully added file '{filePath}' to workspace");
            }
        }

        private void onFileChanged(string filePath, FileChangeType changeType)
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
            if (_documents.TryRemove(filePath, out var documentId))
            {
                _workspace.RemoveDocument(documentId);
                //ToDo: Identify if we need to remove the project here and how
                _logger.LogInformation($"Removed file '{filePath}' from the workspace.");
            }
        }
    }
}
