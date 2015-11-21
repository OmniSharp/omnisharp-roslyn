using System.Composition;
using System.Diagnostics;
using System.IO;
using Microsoft.Framework.ConfigurationModel;
using Microsoft.Framework.Logging;
using OmniSharp.MSBuild;
using OmniSharp.Services;

namespace OmniSharp.Protobuild
{
    [Export(typeof(IProjectSystem))]
    public class ProtobuildProjectSystem : MSBuildProjectSystem
    {
        private readonly IOmnisharpEnvironment _env;
        private readonly MSBuildContext _context;
        private readonly IFileSystemWatcher _watcher;
        private FileSystemWatcher _fsWatcher;

        [ImportingConstructor]
        public ProtobuildProjectSystem(
            OmnisharpWorkspace workspace,
            IOmnisharpEnvironment env,
            ILoggerFactory loggerFactory,
            IEventEmitter emitter,
            IMetadataFileReferenceCache metadataReferenceCache,
            IFileSystemWatcher watcher,
            MSBuildContext context) : base(
                workspace,
                env,
                loggerFactory,
                emitter,
                metadataReferenceCache,
                watcher,
                context)
        {
            _env = env;
            _logger = loggerFactory.CreateLogger<ProtobuildProjectSystem>();
            _context = context;
            _watcher = watcher;
        }
        
        public override string Key { get { return "Protobuild"; } }

        public override void Initalize(IConfiguration configuration)
        {
            _context.AlreadyInitialised = false;

            _logger.LogInformation("Searching for Protobuild in this folder");

            if (!File.Exists(Path.Combine(_env.Path, "Protobuild.exe")))
            {
                _logger.LogInformation("Protobuild.exe not detected in this folder");
                return;
            }

            RunProtobuild();
            
            // Watch for definition file changes.
            if (_watcher.GetType().FullName.Contains("Manual"))
            {
                // We must use a proper filesystem watcher so that we know
                // when definition files change (at the very least, the manual
                // watcher does not appear to work in Visual Studio Code).
                if (_fsWatcher != null)
                {
                    _fsWatcher.Dispose();
                }
                _fsWatcher = new FileSystemWatcher(_env.Path);
                _fsWatcher.IncludeSubdirectories = true;
                _fsWatcher.EnableRaisingEvents = true;
                _fsWatcher.Changed += OnChanged;
                _fsWatcher.Created += OnChanged;
                _fsWatcher.Deleted += OnChanged;
                _fsWatcher.Renamed += (sender, e) =>
                {
                    OnFileChanged(e.OldFullPath);
                    OnFileChanged(e.FullPath);
                };
            }
            else
            {
                _watcher.WatchGlobal(OnFileChanged);
            }

            // Now just initialize the MSBuild project system.
            base.Initalize(configuration);
            _context.AlreadyInitialised = true;
        }
        
        private void RunProtobuild()
        {
            // TODO: Offer the user some way of changing the current platform from IDEs.
            // For now, just assume the host platform.
            string hostPlatform;
            if (Path.DirectorySeparatorChar == '/')
            {
                if (Directory.Exists("/Library"))
                {
                    hostPlatform = "MacOS";
                }
                else
                {
                    hostPlatform = "Linux";
                }
            }
            else
            {
                hostPlatform = "Windows";
            }
            
            _logger.LogInformation("Running Protobuild.exe with --generate " + hostPlatform);

            var startInfo = new ProcessStartInfo();
            startInfo.FileName = Path.Combine(_env.Path, "Protobuild.exe");
            startInfo.Arguments = "--generate " + hostPlatform;
            startInfo.UseShellExecute = false;
            startInfo.WorkingDirectory = _env.Path;
            var process = Process.Start(startInfo);
            process.WaitForExit();

            if (process.ExitCode != 0)
            {
                _logger.LogCritical("Protobuild did not exit successfully (check the log file for details)!");
            }
        }

        private void OnChanged(object sender, FileSystemEventArgs e)
        {
            OnFileChanged(e.FullPath);
        }
        
        private void OnFileChanged(string path)
        {
            if (new FileInfo(path).Extension == ".definition")
            {
                _logger.LogInformation("Detected definition file change; running Protobuild");
                RunProtobuild();
            }
        }
    }
}