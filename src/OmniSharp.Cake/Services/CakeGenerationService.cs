using System;
using System.Composition;
using System.IO;
using Cake.Scripting.Abstractions;
using Cake.Scripting.Abstractions.Models;
using Cake.Scripting.Transport.Tcp.Client;
using Microsoft.Extensions.Logging;
using OmniSharp.Cake.Configuration;

namespace OmniSharp.Cake.Services
{
    [Export(typeof(IScriptGenerationService)), Shared]
    public class CakeGenerationService : IScriptGenerationService
    {
        private readonly IScriptGenerationService _generationService;

        [ImportingConstructor]
        public CakeGenerationService(IOmniSharpEnvironment environment, ICakeConfiguration configuration, ILoggerFactory loggerFactory)
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

            var serverExecutablePath = CakeGenerationServiceToolResolver.GetServerExecutablePath(environment.TargetDirectory, configuration);

            if (File.Exists(serverExecutablePath))
            {
                _generationService = new ScriptGenerationClient(serverExecutablePath, environment.TargetDirectory, loggerFactory);
            }
        }

        public CakeScript Generate(FileChange fileChange)
        {
            return _generationService.Generate(fileChange);
        }
    }
}
