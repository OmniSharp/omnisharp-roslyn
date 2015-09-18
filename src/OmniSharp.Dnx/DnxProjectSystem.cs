using System;
using System.Collections.Generic;
using System.Composition;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNet.Hosting;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.Text;
using Microsoft.Framework.Configuration;
using Microsoft.Framework.DesignTimeHost.Models;
using Microsoft.Framework.DesignTimeHost.Models.IncomingMessages;
using Microsoft.Framework.DesignTimeHost.Models.OutgoingMessages;
#if DNX451
using Microsoft.Framework.FileSystemGlobbing;
#endif
using Microsoft.Framework.Logging;
using Microsoft.Framework.OptionsModel;
using Newtonsoft.Json.Linq;
using OmniSharp.Models;
using OmniSharp.Models.v1;
using OmniSharp.Options;
using OmniSharp.Services;
using OmniSharp.Utilities;

namespace OmniSharp.Dnx
{
    [Export(typeof(IProjectSystem)), Shared]
    public class DnxProjectSystem : IProjectSystem
    {
        private readonly OmnisharpWorkspace _workspace;
        private readonly IOmnisharpEnvironment _env;
        private readonly ILogger _logger;
        private readonly IMetadataFileReferenceCache _metadataFileReferenceCache;
        private DnxPaths _dnxPaths;
        private DesignTimeHostManager _designTimeHostManager;
        private PackagesRestoreTool _packagesRestoreTool;
        private DnxOptions _options;
        private readonly DnxContext _context;
        private readonly IFileSystemWatcher _watcher;
        private readonly IEventEmitter _emitter;
        private readonly DirectoryEnumerator _directoryEnumerator;
        private readonly ILoggerFactory _loggerFactory;

        [ImportingConstructor]
        public DnxProjectSystem(OmnisharpWorkspace workspace,
                                    IOmnisharpEnvironment env,
                                    ILoggerFactory loggerFactory,
                                    IMetadataFileReferenceCache metadataFileReferenceCache,
                                    IApplicationLifetime lifetime,
                                    IFileSystemWatcher watcher,
                                    IEventEmitter emitter,
                                    DnxContext context)
        {
            _workspace = workspace;
            _env = env;
            _loggerFactory = loggerFactory;
            _logger = loggerFactory.CreateLogger<DnxProjectSystem>();
            _metadataFileReferenceCache = metadataFileReferenceCache;
            _context = context;
            _watcher = watcher;
            _emitter = emitter;
            _directoryEnumerator = new DirectoryEnumerator(loggerFactory);

            lifetime?.ApplicationStopping.Register(OnShutdown);
        }

        public string Key { get { return "Dnx"; } }
        public string Language { get { return LanguageNames.CSharp; } }
        public IEnumerable<string> Extensions { get; } = new[] { ".cs" };

        public void Initalize(IConfiguration configuration)
        {
            _options = new DnxOptions();
            ConfigurationBinder.Bind(configuration, _options);

            _dnxPaths = new DnxPaths(_env, _options, _loggerFactory);
            _packagesRestoreTool = new PackagesRestoreTool(_options, _loggerFactory, _emitter, _context, _dnxPaths); ;
            _designTimeHostManager = new DesignTimeHostManager(_loggerFactory, _dnxPaths);

            var runtimePath = _dnxPaths.RuntimePath;
            _context.RuntimePath = runtimePath.Value;
            _context.Options = _options;


            if (!ScanForProjects())
            {
                // No DNX projects found so do nothing
                _logger.LogInformation("No project.json based projects found");
                return;
            }

            if (_context.RuntimePath == null)
            {
                // There is no default dnx found so do nothing
                _logger.LogInformation("No default runtime found");
                _emitter.Emit(EventTypes.Error, runtimePath.Error);
                return;
            }

            var wh = new ManualResetEventSlim();

            _designTimeHostManager.Start(_context.HostId, port =>
            {
                var socket = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);
                socket.Connect(new IPEndPoint(IPAddress.Loopback, port));

                var networkStream = new NetworkStream(socket);

                _logger.LogInformation("Connected");

                _context.DesignTimeHostPort = port;

                _context.Connection = new ProcessingQueue(networkStream, _logger);

                _context.Connection.OnReceive += m =>
                {
                    var project = _context.Projects[m.ContextId];

                    if (m.MessageType == "ProjectInformation")
                    {
                        var val = m.Payload.ToObject<ProjectMessage>();

                        project.Name = val.Name;
                        project.GlobalJsonPath = val.GlobalJsonPath;
                        project.Configurations = val.Configurations;
                        project.Commands = val.Commands;
                        project.ProjectSearchPaths = val.ProjectSearchPaths;

                        this._emitter.Emit(EventTypes.ProjectChanged, new ProjectInformationResponse()
                        {
                            {nameof(DnxProject), new DnxProject(project)}
                        });

                        var unprocessed = project.ProjectsByFramework.Keys.ToList();

                        foreach (var frameworkData in val.Frameworks)
                        {
                            unprocessed.Remove(frameworkData.FrameworkName);

                            var frameworkProject = project.ProjectsByFramework.GetOrAdd(frameworkData.FrameworkName, framework =>
                            {
                                return new FrameworkProject(project, frameworkData);
                            });

                            var id = frameworkProject.ProjectId;

                            if (_workspace.CurrentSolution.ContainsProject(id))
                            {
                                continue;
                            }
                            else
                            {
                                var projectInfo = ProjectInfo.Create(
                                        id,
                                        VersionStamp.Create(),
                                        val.Name + "+" + frameworkData.ShortName,
                                        val.Name,
                                        LanguageNames.CSharp,
                                        project.Path);

                                _workspace.AddProject(projectInfo);
                                _context.WorkspaceMapping[id] = frameworkProject;
                            }

                            lock (frameworkProject.PendingProjectReferences)
                            {
                                var reference = new Microsoft.CodeAnalysis.ProjectReference(id);

                                foreach (var referenceId in frameworkProject.PendingProjectReferences)
                                {
                                    _workspace.AddProjectReference(referenceId, reference);
                                }

                                frameworkProject.PendingProjectReferences.Clear();
                            }

                        }

                        // Remove old projects
                        foreach (var frameworkName in unprocessed)
                        {
                            FrameworkProject frameworkProject;
                            project.ProjectsByFramework.TryRemove(frameworkName, out frameworkProject);
                            _workspace.RemoveProject(frameworkProject.ProjectId);
                        }
                    }
                    // This is where we can handle messages and update the
                    // language service
                    else if (m.MessageType == "References")
                    {
                        // References as well as the dependency graph information
                        var val = m.Payload.ToObject<ReferencesMessage>();

                        var frameworkProject = project.ProjectsByFramework[val.Framework.FrameworkName];
                        var projectId = frameworkProject.ProjectId;

                        var metadataReferences = new List<MetadataReference>();
                        var projectReferences = new List<Microsoft.CodeAnalysis.ProjectReference>();

                        var removedFileReferences = frameworkProject.FileReferences.ToDictionary(p => p.Key, p => p.Value);
                        var removedRawReferences = frameworkProject.RawReferences.ToDictionary(p => p.Key, p => p.Value);
                        var removedProjectReferences = frameworkProject.ProjectReferences.ToDictionary(p => p.Key, p => p.Value);

                        foreach (var file in val.FileReferences)
                        {
                            if (removedFileReferences.Remove(file))
                            {
                                continue;
                            }

                            var metadataReference = _metadataFileReferenceCache.GetMetadataReference(file);
                            frameworkProject.FileReferences[file] = metadataReference;
                            metadataReferences.Add(metadataReference);
                        }

                        foreach (var rawReference in val.RawReferences)
                        {
                            if (removedRawReferences.Remove(rawReference.Key))
                            {
                                continue;
                            }

                            var metadataReference = MetadataReference.CreateFromImage(rawReference.Value);
                            frameworkProject.RawReferences[rawReference.Key] = metadataReference;
                            metadataReferences.Add(metadataReference);
                        }

                        foreach (var projectReference in val.ProjectReferences)
                        {
                            if (removedProjectReferences.Remove(projectReference.Path))
                            {
                                continue;
                            }

                            int projectReferenceContextId;
                            if (!_context.ProjectContextMapping.TryGetValue(projectReference.Path, out projectReferenceContextId))
                            {
                                projectReferenceContextId = AddProject(projectReference.Path);
                            }

                            var referencedProject = _context.Projects[projectReferenceContextId];

                            var referencedFrameworkProject = referencedProject.ProjectsByFramework.GetOrAdd(projectReference.Framework.FrameworkName,
                                framework =>
                                {
                                    return new FrameworkProject(referencedProject, projectReference.Framework);
                                });

                            var projectReferenceId = referencedFrameworkProject.ProjectId;

                            if (_workspace.CurrentSolution.ContainsProject(projectReferenceId))
                            {
                                projectReferences.Add(new Microsoft.CodeAnalysis.ProjectReference(projectReferenceId));
                            }
                            else
                            {
                                lock (referencedFrameworkProject.PendingProjectReferences)
                                {
                                    referencedFrameworkProject.PendingProjectReferences.Add(projectId);
                                }
                            }

                            referencedFrameworkProject.ProjectDependeees[project.Path] = projectId;

                            frameworkProject.ProjectReferences[projectReference.Path] = projectReferenceId;
                        }

                        foreach (var reference in metadataReferences)
                        {
                            _workspace.AddMetadataReference(projectId, reference);
                        }

                        foreach (var projectReference in projectReferences)
                        {
                            _workspace.AddProjectReference(projectId, projectReference);
                        }

                        foreach (var pair in removedProjectReferences)
                        {
                            _workspace.RemoveProjectReference(projectId, new Microsoft.CodeAnalysis.ProjectReference(pair.Value));
                            frameworkProject.ProjectReferences.Remove(pair.Key);

                            // TODO: Update the dependee's list
                        }

                        foreach (var pair in removedFileReferences)
                        {
                            _workspace.RemoveMetadataReference(projectId, pair.Value);
                            frameworkProject.FileReferences.Remove(pair.Key);
                        }

                        foreach (var pair in removedRawReferences)
                        {
                            _workspace.RemoveMetadataReference(projectId, pair.Value);
                            frameworkProject.RawReferences.Remove(pair.Key);
                        }
                    }
                    else if (m.MessageType == "Dependencies")
                    {
                        var val = m.Payload.ToObject<DependenciesMessage>();
                        var unresolvedDependencies = val.Dependencies.Values
                            .Where(dep => dep.Type == "Unresolved");

                        if (unresolvedDependencies.Any())
                        {
                            _logger.LogInformation("Project {0} has these unresolved references: {1}", project.Path, string.Join(", ", unresolvedDependencies.Select(d => d.Name)));
                            _emitter.Emit(EventTypes.UnresolvedDependencies, new UnresolvedDependenciesMessage()
                            {
                                FileName = project.Path,
                                UnresolvedDependencies = unresolvedDependencies.Select(d => new PackageDependency() { Name = d.Name, Version = d.Version })
                            });
                            _packagesRestoreTool.Run(project);
                        }
                    }
                    else if (m.MessageType == "CompilerOptions")
                    {
                        // Configuration and compiler options
                        var val = m.Payload.ToObject<CompilationOptionsMessage>();

                        var projectId = project.ProjectsByFramework[val.Framework.FrameworkName].ProjectId;

                        var options = val.CompilationOptions.CompilationOptions;

                        var specificDiagnosticOptions = options.SpecificDiagnosticOptions
                        .ToDictionary(p => p.Key, p => (ReportDiagnostic)p.Value);

                        var csharpOptions = new CSharpCompilationOptions(
                                outputKind: (OutputKind)options.OutputKind,
                                optimizationLevel: (OptimizationLevel)options.OptimizationLevel,
                                platform: (Platform)options.Platform,
                                generalDiagnosticOption: (ReportDiagnostic)options.GeneralDiagnosticOption,
                                warningLevel: options.WarningLevel,
                                allowUnsafe: options.AllowUnsafe,
                                concurrentBuild: options.ConcurrentBuild,
                                specificDiagnosticOptions: specificDiagnosticOptions
                            );

                        var parseOptions = new CSharpParseOptions(val.CompilationOptions.LanguageVersion,
                                                                  preprocessorSymbols: val.CompilationOptions.Defines);

                        _workspace.SetCompilationOptions(projectId, csharpOptions);
                        _workspace.SetParseOptions(projectId, parseOptions);
                    }
                    else if (m.MessageType == "Sources")
                    {
                        // The sources to feed to the language service
                        var val = m.Payload.ToObject<SourcesMessage>();

                        project.SourceFiles = val.Files
                            .Where(fileName => Path.GetExtension(fileName) == ".cs")
                            .ToList();

                        var frameworkProject = project.ProjectsByFramework[val.Framework.FrameworkName];
                        var projectId = frameworkProject.ProjectId;

                        var unprocessed = new HashSet<string>(frameworkProject.Documents.Keys);

                        foreach (var file in project.SourceFiles)
                        {
                            if (unprocessed.Remove(file))
                            {
                                continue;
                            }

                            using (var stream = File.OpenRead(file))
                            {
                                var sourceText = SourceText.From(stream, encoding: Encoding.UTF8);
                                var id = DocumentId.CreateNewId(projectId);
                                var version = VersionStamp.Create();

                                frameworkProject.Documents[file] = id;

                                var loader = TextLoader.From(TextAndVersion.Create(sourceText, version));
                                _workspace.AddDocument(DocumentInfo.Create(id, file, filePath: file, loader: loader));
                            }
                        }

                        foreach (var file in unprocessed)
                        {
                            var docId = frameworkProject.Documents[file];
                            frameworkProject.Documents.Remove(file);
                            _workspace.RemoveDocument(docId);
                        }

                        frameworkProject.Loaded = true;
                    }
                    else if (m.MessageType == "Error")
                    {
                        var val = m.Payload.ToObject<Microsoft.Framework.DesignTimeHost.Models.OutgoingMessages.ErrorMessage>();
                        _logger.LogError(val.Message);
                    }

                    if (project.ProjectsByFramework.Values.All(p => p.Loaded))
                    {
                        wh.Set();
                    }
                };

                // Start the message channel
                _context.Connection.Start();

                // Initialize the DNX projects
                Initialize();
            });

            wh.Wait();
        }

        public Project GetProject(string path)
        {
            int contextId;
            if (!_context.ProjectContextMapping.TryGetValue(path, out contextId))
            {
                return null;
            }


            return _context.Projects[contextId];
        }

        private void OnShutdown()
        {
            _designTimeHostManager.Stop();
        }

        private void TriggerDependeees(string path, string messageType)
        {
            // temp: run [dnu|kpm] restore when project.json changed
            var project = GetProject(path);
            if (project != null)
            {
                _packagesRestoreTool.Run(project);
            }

            var seen = new HashSet<string>();
            var results = new HashSet<int>();
            var stack = new Stack<string>();

            stack.Push(path);

            while (stack.Count > 0)
            {
                var projectPath = stack.Pop();

                if (!seen.Add(projectPath))
                {
                    continue;
                }

                int contextId;
                if (_context.ProjectContextMapping.TryGetValue(projectPath, out contextId))
                {
                    results.Add(contextId);

                    foreach (var frameworkProject in _context.Projects[contextId].ProjectsByFramework.Values)
                    {
                        foreach (var dependee in frameworkProject.ProjectDependeees.Keys)
                        {
                            stack.Push(dependee);
                        }
                    }
                }
            }

            foreach (var contextId in results)
            {
                var message = new Message();
                message.HostId = _context.HostId;
                message.ContextId = contextId;
                message.MessageType = messageType;
                _context.Connection.Post(message);
            }
        }

        private void WatchProject(string projectFile)
        {
            // Whenever the project file changes, trigger FilesChanged to the design time host
            // and all dependendees of the project. That means if A -> B -> C
            // if C changes, notify A and B
            _watcher.Watch(projectFile, path => TriggerDependeees(path, "FilesChanged"));

            // When the project.lock.json file changes, refresh dependencies
            var lockFile = Path.ChangeExtension(projectFile, "lock.json");

            _watcher.Watch(lockFile, _ => TriggerDependeees(projectFile, "RefreshDependencies"));
        }

        private void Initialize()
        {
            foreach (var project in _context.Projects.Values)
            {
                if (project.InitializeSent)
                {
                    continue;
                }

                WatchProject(project.Path);

                var projectDirectory = Path.GetDirectoryName(project.Path).TrimEnd(Path.DirectorySeparatorChar);

                // Send an InitializeMessage for each project
                var initializeMessage = new InitializeMessage
                {
                    ProjectFolder = projectDirectory,
                };

                // Initialize this project
                _context.Connection.Post(new Message
                {
                    ContextId = project.ContextId,
                    MessageType = "Initialize",
                    Payload = JToken.FromObject(initializeMessage),
                    HostId = _context.HostId
                });

                project.InitializeSent = true;
            }
        }

        private int AddProject(string projectFile)
        {
            Project project;
            if (!_context.TryAddProject(projectFile, out project))
            {
                return project.ContextId;
            }

            WatchProject(projectFile);

            // Send an InitializeMessage for each project
            var initializeMessage = new InitializeMessage
            {
                ProjectFolder = Path.GetDirectoryName(projectFile),
            };

            // Initialize this project
            _context.Connection.Post(new Message
            {
                ContextId = project.ContextId,
                MessageType = "Initialize",
                Payload = JToken.FromObject(initializeMessage),
                HostId = _context.HostId
            });

            project.InitializeSent = true;
            return project.ContextId;
        }

        private bool ScanForProjects()
        {
            _logger.LogInformation(string.Format("Scanning '{0}' for DNX projects", _env.Path));

            var anyProjects = false;

            // Single project in this folder
            var projectInThisFolder = Path.Combine(_env.Path, "project.json");

            if (File.Exists(projectInThisFolder))
            {
                if (_context.TryAddProject(projectInThisFolder))
                {
                    _logger.LogInformation(string.Format("Found project '{0}'.", projectInThisFolder));

                    anyProjects = true;
                }
            }
            else
            {
                IEnumerable<string> paths;
#if DNX451
                if (_options.Projects != "**/project.json")
                {
                    var matcher = new Matcher();
                    matcher.AddIncludePatterns(_options.Projects.Split(';'));
                    paths = matcher.GetResultsInFullPath(_env.Path);
                }
                else
                {
                    paths = _directoryEnumerator.SafeEnumerateFiles(_env.Path, "project.json");
                }
#else
                // The matcher works on CoreCLR but Omnisharp still targets aspnetcore50 instead of
                // dnxcore50
                paths = _directoryEnumerator.SafeEnumerateFiles(_env.Path, "project.json");
#endif
                foreach (var path in paths)
                {
                    string projectFile = null;

                    if (Path.GetFileName(path) == "project.json")
                    {
                        projectFile = path;
                    }
                    else
                    {
                        projectFile = Path.Combine(path, "project.json");
                        if (!File.Exists(projectFile))
                        {
                            projectFile = null;
                        }
                    }

                    if (string.IsNullOrEmpty(projectFile))
                    {
                        continue;
                    }

                    if (!_context.TryAddProject(projectFile))
                    {
                        continue;
                    }

                    _logger.LogInformation(string.Format("Found project '{0}'.", projectFile));

                    anyProjects = true;
                }
            }

            return anyProjects;
        }

        private static Task ConnectAsync(Socket socket, IPEndPoint endPoint)
        {
            return Task.Factory.FromAsync((cb, state) => socket.BeginConnect(endPoint, cb, state), ar => socket.EndConnect(ar), null);
        }

        Task<object> IProjectSystem.GetProjectModel(string path)
        {
            var document = _workspace.GetDocument(path);
            if (document == null)
                return Task.FromResult<object>(null);

            var project = GetProject(document.Project.FilePath);
            if (project == null)
                return Task.FromResult<object>(null);

            return Task.FromResult<object>(new DnxProject(project));
        }

        Task<object> IProjectSystem.GetInformationModel(WorkspaceInformationRequest request)
        {
            return Task.FromResult<object>(new DnxWorkspaceInformation(_context));
        }
    }
}
