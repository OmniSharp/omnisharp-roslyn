using Microsoft.AspNet.Hosting;
using Microsoft.Framework.Cache.Memory;
using Microsoft.Framework.ConfigurationModel;
using Microsoft.Framework.Logging;
using Microsoft.Framework.OptionsModel;
using Newtonsoft.Json;
using OmniSharp.AspNet5;
using OmniSharp.MSBuild;
using OmniSharp.Models;
using OmniSharp.Options;
using OmniSharp.Services;
using OmniSharp.Stdio.Protocol;
using System;
using System.IO;
using System.Threading.Tasks;

namespace OmniSharp.Stdio
{
    public class Program
    {
        private readonly IServiceProvider _serviceProvider;

        public Program(IServiceProvider serviceProvider)
        {
            _serviceProvider = serviceProvider;
        }

        public async Task Main(string[] args)
        {
            var applicationRoot = Directory.GetCurrentDirectory();
            var logLevel = LogLevel.Information;
            var hostPID = -1;

            var enumerator = args.GetEnumerator();

            while (enumerator.MoveNext())
            {
                var arg = (string)enumerator.Current;
                if (arg == "-s")
                {
                    enumerator.MoveNext();
                    applicationRoot = Path.GetFullPath((string)enumerator.Current);
                }
                else if (arg == "-v")
                {
                    logLevel = LogLevel.Verbose;
                }
                else if (arg == "--hostPID")
                {
                    enumerator.MoveNext();
                    hostPID = int.Parse((string)enumerator.Current);
                }
            }

            var config = new Configuration().AddEnvironmentVariables().AddJsonFile("config.json");
            var omnisharpOptions = new OptionsManager<OmniSharpOptions>(new[] { new ConfigureFromConfigurationOptions<OmniSharpOptions>(config) });
            var memoryCacheOptions = new OptionsManager<MemoryCacheOptions>(new[] { new ConfigureFromConfigurationOptions<MemoryCacheOptions>(config) });

            var env = new OmnisharpEnvironment(applicationRoot, -1, hostPID, logLevel);
            var watcher = new FileSystemWatcherWrapper(env);
            var workspace = new OmnisharpWorkspace();
            var lifetime = new ApplicationLifetime();
            var loggerFactory = new LoggerFactory();
            var memoryCache = new MemoryCache(memoryCacheOptions);
            var metadataFileReferenceCache = new MetadataFileReferenceCache(memoryCache, loggerFactory);

            var aspContext = new AspNet5Context();
            var aspProjectSystem = new AspNet5ProjectSystem(workspace, env, omnisharpOptions, 
                loggerFactory, metadataFileReferenceCache, lifetime, watcher, aspContext);

            var msbuildContext = new MSBuildContext();
            var msbuildProjectSystem = new MSBuildProjectSystem(workspace, env, loggerFactory, 
                metadataFileReferenceCache, watcher, msbuildContext);

            aspProjectSystem.Initalize();
            msbuildProjectSystem.Initalize();
            workspace.Initialized = true;

            var controller = new OmnisharpController(workspace, omnisharpOptions);

            while (true)
            {
                var line = Console.ReadLine();
                RequestPacket req = JsonConvert.DeserializeObject<RequestPacket>(line);
                ResponsePacket res = req.Reply(null);
                res.Success = true;

                switch(req.Command)
                {
                    case "typelookup":
                        res.Body = await controller.TypeLookup(new TypeLookupRequest()
                        {
                            IncludeDocumentation = true,
                            Line = req.Arguments.Line,
                            Column = req.Arguments.Column,
                            FileName = req.Arguments.FileName
                        });
                        break;
                    case "gotodefinition":
                        res.Body = await controller.GotoDefinition(new Request()
                        {
                            Line = req.Arguments.Line,
                            Column = req.Arguments.Column,
                            FileName = req.Arguments.FileName
                        });
                        break;
                    case "autocomplete":
                        res.Body = await controller.AutoComplete(new AutoCompleteRequest()
                        {
                            Line = req.Arguments.Line,
                            Column = req.Arguments.Column,
                            FileName = req.Arguments.FileName
                        });
                        break;
                    default:
                        res.Success = false;
                        res.Message = "Unknown command";
                        break;
                }

                Console.WriteLine(res);
            }
        }
    }
}
