using System;
using System.Composition;
using System.IO;
using System.Linq;
using Cake.Scripting.Abstractions;
using Cake.Scripting.Abstractions.Models;
using Cake.Scripting.Transport.Tcp.Client;
using Microsoft.Extensions.Logging;
using OmniSharp.Cake.Configuration;

namespace OmniSharp.Cake.Services
{
    [Export(typeof(ICakeScriptService)), Shared]
    public sealed class CakeScriptService : ICakeScriptService
    {
        private readonly IScriptGenerationService _generationService;

        [ImportingConstructor]
        public CakeScriptService(IOmniSharpEnvironment environment, ICakeConfiguration configuration, ILoggerFactory loggerFactory)
        {
            if (environment == null)
            {
                throw new ArgumentNullException(nameof(environment));
            }
            if (configuration == null)
            {
                throw new ArgumentNullException(nameof(configuration));
            }
            if (loggerFactory == null)
            {
                throw new ArgumentNullException(nameof(loggerFactory));
            }

            var serverExecutablePath = ScriptGenerationToolResolver.GetExecutablePath(environment.TargetDirectory, configuration);

            if (File.Exists(serverExecutablePath))
            {
                _generationService = new ScriptGenerationClient(serverExecutablePath, environment.TargetDirectory, loggerFactory);
            }

            IsConnected = _generationService != null;
        }

        public CakeScript Generate(FileChange fileChange)
        {
            var cakeScript = _generationService.Generate(fileChange);

            // TODO: Cache these, don't invoke if nothing changed
            OnReferencesChanged(new ReferencesChangedEventArgs(fileChange.FileName, cakeScript.References.ToList()));
            OnUsingsChanged(new UsingsChangedEventArgs(fileChange.FileName, cakeScript.Usings.ToList()));

            return cakeScript;
        }

        public bool IsConnected { get; }
        public event EventHandler<ReferencesChangedEventArgs> ReferencesChanged;
        public event EventHandler<UsingsChangedEventArgs> UsingsChanged;

        private void OnReferencesChanged(ReferencesChangedEventArgs e)
        {
            ReferencesChanged?.Invoke(this, e);
        }

        private void OnUsingsChanged(UsingsChangedEventArgs e)
        {
            UsingsChanged?.Invoke(this, e);
        }
    }
}
