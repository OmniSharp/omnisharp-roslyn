using Microsoft.AspNet.Hosting;
using Microsoft.AspNet.Mvc;
using Microsoft.Framework.Cache.Memory;
using Microsoft.Framework.ConfigurationModel;
using Microsoft.Framework.Logging;
using Microsoft.Framework.OptionsModel;
using Newtonsoft.Json;
using OmniSharp.Models;
using OmniSharp.Options;
using OmniSharp.Services;
using OmniSharp.Stdio.Protocol;
using System;
using System.Linq;
using System.IO;
using System.Threading.Tasks;
using System.Collections.Generic;
using Microsoft.Framework.DependencyInjection;
using Microsoft.Framework.DependencyInjection.Fallback;
using System.Reflection;

namespace OmniSharp.Stdio
{

    static class Controllers
    {
        public readonly static IDictionary<string, MethodInfo> Routes;

        public readonly static IEnumerable<Type> Types;

        static Controllers()
        {
            Types = new[] {
                typeof(OmnisharpController),
                typeof(ProjectSystemController),
                typeof(CodeActionController)
            };
            Routes = new Dictionary<string, MethodInfo>(StringComparer.OrdinalIgnoreCase);

            foreach (var type in Types)
            {
                foreach (var method in type.GetMethods())
                {
                    var attribute = method.GetCustomAttribute<HttpPostAttribute>();
                    if (attribute != null)
                    {
                        Routes[attribute.Template.TrimStart('/')] = method;
                    }
                }
            }
        }
    }
    
    public class Program
    {
        private readonly IServiceProvider _serviceProvider;

        public Program(IServiceProvider serviceProvider)
        {
            _serviceProvider = serviceProvider;
        }

        public void Main(string[] args)
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

            OmniSharp.Program.Environment = new OmnisharpEnvironment(applicationRoot, -1, hostPID, logLevel);

            var services = new ServiceCollection();
            var lifetime = new ApplicationLifetime();

            services.AddSingleton<ILoggerFactory, LoggerFactory>();

            var startup = new OmniSharp.Startup();
            startup.ConfigureServices(services, lifetime);

            // register controllers
            foreach(var type in Controllers.Types)
            {
                services.AddTransient(type);
                Console.WriteLine(string.Format("Added controller -> {0}", type.Name));
            }

            var provider = services.BuildServiceProvider();

            var projectSystems = provider.GetRequiredService<IEnumerable<IProjectSystem>>();
            foreach (var projectSystem in projectSystems)
            {
                projectSystem.Initalize();
            }

            // Mark the workspace as initialized
            startup.Workspace.Initialized = true;

            Console.WriteLine("Reading from Stdin");

            while (true)
            {
                RequestPacket req;
                try
                {
                    var line = Console.ReadLine();
                    req = JsonConvert.DeserializeObject<RequestPacket>(line);
                }
                catch (Exception e)
                {
                    // Todo@jo - error event
                    Console.WriteLine(e);
                    continue;
                }
                
                 HandleRequest(req, provider);
            }
        }

        private void HandleRequest(RequestPacket req, IServiceProvider provider)
        {
            Task.Factory.StartNew(async () =>
            {
                ResponsePacket res = req.Reply(null);
                MethodInfo target;
    
                if (Controllers.Routes.TryGetValue(req.Command, out target))
                {
                    try
                    {
                        res.Success = true;
                        res.Running = true;

                        var controller = provider.GetRequiredService(target.DeclaringType);
                        object result = null;
                        if (target.GetParameters().Length == 1)
                        {
                            // hack!
                            var body = JsonConvert.DeserializeObject(JsonConvert.SerializeObject(req.Arguments), target.GetParameters()[0].ParameterType);
                            result = target.Invoke(controller, new object[] { body });
                        }
                        else if (target.GetParameters().Length == 0)
                        {
                            result = target.Invoke(controller, new object[] { });
                        }
                        else
                        {
                            res.Success = false;
                            res.Message = target.ToString();
                        }
                        
                        if (result is Task)
                        {
                            var task = (Task)result;
                            await task;
                            res.Body = task.GetType().GetProperty("Result").GetValue(task);
                        }
                        else
                        {
                            res.Body = result;
                        }
                    }
                    catch (Exception e)
                    {
                        Console.WriteLine(e);
                        res.Success = false;
                        res.Message = e.ToString();
                    }
                }
                else
                {
                    res.Success = false;
                    res.Message = "Unknown command";
                }
                Console.WriteLine(res);
            });
        }
    }
}
