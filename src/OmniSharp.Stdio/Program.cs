using Microsoft.AspNet.Hosting;
using Microsoft.Framework.Cache.Memory;
using Microsoft.Framework.ConfigurationModel;
using Microsoft.Framework.Logging;
using Microsoft.Framework.OptionsModel;
using Microsoft.Framework.Runtime;
using OmniSharp.AspNet5;
using OmniSharp.MSBuild;
using OmniSharp.Services;
using System;
using System.IO;
using System.Threading;
using System.Collections.Generic;
using OmniSharp.Options;
using Newtonsoft.Json;
using OmniSharp.Stdio.Protocol;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using OmniSharp.Models;

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

            var controller1 = new OmnisharpController(workspace, omnisharpOptions);
            var controller2 = new ProjectSystemController(aspContext, msbuildContext);

            var regExpQuickInfo = new Regex(@"^quickinfo (\d+) (\d+) (.*)$");
            var gotoDefinitionBuffer = new Regex(@"^definition (\d+) (\d+) (.*)$");

            Console.WriteLine("Ready");

            while (true)
            {
                var readLine = Console.ReadLine();
                MatchCollection match;
                ResponsePacket res = null;

                if ((match = regExpQuickInfo.Matches(readLine)).Count > 0)
                {
                    var line = int.Parse(match[0].Groups[1].Value);
                    var column = int.Parse(match[0].Groups[2].Value);
                    var filename = match[0].Groups[3].Value;

                    var item = await controller1.TypeLookup(new Models.TypeLookupRequest()
                    {
                        FileName = filename,
                        Line = line,
                        Column = column,
                        IncludeDocumentation = true
                    });

                    res = new ResponsePacket()
                    {
                        Command = "quickinfo",
                        Running = true,
                        Success = true,
                        Request_seq = 0,
                        Body = item
                    };

                }
                else if((match = gotoDefinitionBuffer.Matches(readLine)).Count > 0)
                {
                    var line = int.Parse(match[0].Groups[1].Value);
                    var column = int.Parse(match[0].Groups[2].Value);
                    var filename = match[0].Groups[3].Value;
                    var item = await controller1.GotoDefinition(new Request()
                    {
                        FileName = filename,
                        Line = line,
                        Column = column
                    });

                    res = new ResponsePacket()
                    {
                        Command = "definition",
                        Running = true,
                        Success = true, 
                        Request_seq = 0, 
                        Body = item
                    };
                }
                else
                {
                    Console.WriteLine(string.Format("?{0}", readLine));
                }

                if (res != null) {
                    Console.WriteLine(JsonConvert.SerializeObject(res));
                }
            }
        }
    }
}
