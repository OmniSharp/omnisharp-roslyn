using System;
using System.Composition;
using System.IO;
using Cake.Scripting.Abstractions;
using Cake.Scripting.Abstractions.Models;
using Cake.Scripting.Transport.Tcp.Client;
using Microsoft.Extensions.Logging;

namespace OmniSharp.Cake.Services
{
    [Export(typeof(IScriptGenerationService)), Shared]
    public class CakeGenerationService : IScriptGenerationService
    {
        private readonly IScriptGenerationService _generationService;

        [ImportingConstructor]
        public CakeGenerationService(IOmniSharpEnvironment environment, ILoggerFactory loggerFactory)
        {
            if (environment == null)
            {
                throw new ArgumentNullException(nameof(environment));
            }
            if (loggerFactory == null)
            {
                throw new ArgumentNullException(nameof(loggerFactory));
            }

            // TODO: Read from cake configuration, don't assume Bakery is in tools folder
            var bakeryExe = Path.Combine(environment.TargetDirectory, "tools", "Cake.Bakery", "tools", "Cake.Bakery.exe");
            _generationService = new ScriptGenerationClient(bakeryExe, environment.TargetDirectory, loggerFactory);
        }

        public CakeScript Generate(FileChange fileChange)
        {
            return _generationService.Generate(fileChange);
        }
    }
}
