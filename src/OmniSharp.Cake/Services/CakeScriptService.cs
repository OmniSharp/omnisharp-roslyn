using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Composition;
using System.IO;
using System.Linq;
using Cake.Scripting.Abstractions;
using Cake.Scripting.Abstractions.Models;
using Cake.Scripting.Transport.Tcp.Client;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using OmniSharp.Cake.Configuration;
using OmniSharp.Utilities;

namespace OmniSharp.Cake.Services
{
    [Export(typeof(ICakeScriptService)), Shared]
    public sealed class CakeScriptService : ICakeScriptService, IDisposable
    {
        private readonly IOmniSharpEnvironment _environment;
        private readonly ICakeConfiguration _cakeConfiguration;
        private readonly ILoggerFactory _loggerFactory;
        private readonly IDictionary<string, ISet<string>> _cachedReferences;
        private readonly IDictionary<string, ISet<string>> _cachedUsings;
        private ScriptGenerationClient _generationService;

        [ImportingConstructor]
        public CakeScriptService(IOmniSharpEnvironment environment, ICakeConfiguration cakeConfiguration, ILoggerFactory loggerFactory)
        {
            _environment = environment ?? throw new ArgumentNullException(nameof(environment));
            _cakeConfiguration = cakeConfiguration ?? throw new ArgumentNullException(nameof(cakeConfiguration));
            _loggerFactory = loggerFactory ?? throw new ArgumentNullException(nameof(loggerFactory));
            _cachedReferences = new Dictionary<string, ISet<string>>();
            _cachedUsings = new Dictionary<string, ISet<string>>();
        }

        public bool Initialize(CakeOptions options)
        {
            var serverExecutablePath = ScriptGenerationToolResolver.GetExecutablePath(_environment.TargetDirectory, _cakeConfiguration, options);

            if (File.Exists(serverExecutablePath))
            {
                _generationService = PlatformHelper.IsMono ?
                    new ScriptGenerationClient(new MonoScriptGenerationProcess(serverExecutablePath, _environment, _loggerFactory), _environment.TargetDirectory, _loggerFactory) :
                    new ScriptGenerationClient(serverExecutablePath, _environment.TargetDirectory, _loggerFactory);
            }

            return _generationService != null;
        }

        public CakeScript Generate(FileChange fileChange)
        {
            if (_generationService == null)
            {
                throw new InvalidOperationException("Service not initialized.");
            }

            var cakeScript = _generationService.Generate(fileChange);

            // Set line processor for generated aliases. TODO: Move to Cake.Bakery
            cakeScript.Source = cakeScript.Source.Insert(0, $"{Constants.Directive.Generated}\n");

            // Check if references changed
            if (!_cachedReferences.TryGetValue(fileChange.FileName, out var references))
            {
                references = new HashSet<string>();
            }
            if (!cakeScript.References.SetEquals(references))
            {
                _cachedReferences[fileChange.FileName] = cakeScript.References;
                OnReferencesChanged(new ReferencesChangedEventArgs(fileChange.FileName, cakeScript.References));
            }

            // Check if usings changed
            if (!_cachedUsings.TryGetValue(fileChange.FileName, out var usings))
            {
                usings = new HashSet<string>();
            }
            if (!cakeScript.Usings.SetEquals(usings))
            {
                _cachedUsings[fileChange.FileName] = cakeScript.Usings;
                OnUsingsChanged(new UsingsChangedEventArgs(fileChange.FileName, cakeScript.Usings.ToList()));
            }

            return cakeScript;
        }

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

        public void Dispose()
        {
            _generationService?.Dispose();
        }
    }
}
