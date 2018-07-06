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
            AddMiscellanousFiles(allFiles);
        }

        private async void AddMiscellanousFiles(IEnumerable<string> allFiles)
        {
            foreach (var filePath in allFiles)
            {
                //wait for the project system to get initialised
                await _projectSystem.GetProjectModelAsync(filePath);
                _workspace.AddMiscellanousFile(filePath, Language);
            }
        }
    }
}
