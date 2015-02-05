using System;
using System.Collections.Generic;
using Microsoft.AspNet.Builder;
using Microsoft.AspNet.Diagnostics;
using Microsoft.AspNet.Hosting;
using Microsoft.AspNet.Mvc;
using Microsoft.Framework.Cache.Memory;
using Microsoft.Framework.ConfigurationModel;
using Microsoft.Framework.DependencyInjection;
using Microsoft.Framework.Logging;
using Microsoft.Framework.Logging.Console;
using OmniSharp.AspNet5;
using OmniSharp.Middleware;
using OmniSharp.MSBuild;
using OmniSharp.Options;
using OmniSharp.Services;

namespace OmniSharp
{
    public class Startup
    {
        public Startup()
        {
            Configuration = new Configuration()
                .AddJsonFile("config.json")
                .AddEnvironmentVariables();
        }

        public IConfiguration Configuration { get; private set; }

        public OmnisharpWorkspace Workspace { get; set; }

        public void ConfigureServices(IServiceCollection services, IApplicationLifetime liftime)
        {
            Workspace = new OmnisharpWorkspace();


            // Working around another bad bug in ASP.NET 5
            // https://github.com/aspnet/Hosting/issues/151
            services.AddInstance(liftime);

            // This is super hacky by it's the easiest way to flow serivces from the 
            // hosting layer, this needs to be easier
            services.AddInstance<IOmnisharpEnvironment>(Program.Environment);

            services.AddMvc(Configuration);

            services.Configure<MvcOptions>(opt =>
            {
                opt.OutputFormatters.RemoveAll(r => r.Instance is XmlOutputFormatter);
            });

            // Add the omnisharp workspace to the container
            services.AddInstance(Workspace);

            // Caching
            services.AddSingleton<IMemoryCache, MemoryCache>();
            services.AddSingleton<IMetadataFileReferenceCache, MetadataFileReferenceCache>();

            // Add the project systems
            services.AddInstance(new AspNet5Context());
            services.AddInstance(new MSBuildContext());

            services.AddSingleton<IProjectSystem, AspNet5ProjectSystem>();
            services.AddSingleton<IProjectSystem, MSBuildProjectSystem>();

            // Add the file watcher
            services.AddSingleton<IFileSystemWatcher, FileSystemWatcherWrapper>();

            // Add test command providers
            services.AddSingleton<ITestCommandProvider, AspNet5TestCommandProvider>();

#if ASPNET50
            // Add the code action provider
            services.AddSingleton<ICodeActionProvider, EmptyCodeActionProvider>();
#else
            services.AddSingleton<ICodeActionProvider, EmptyCodeActionProvider>();
#endif

            // Setup the options from configuration
            services.Configure<OmniSharpOptions>(Configuration);
        }

        public void Configure(IApplicationBuilder app,
                              ILoggerFactory loggerFactory,
                              IOmnisharpEnvironment env)
        {
            loggerFactory.AddConsole((category, type) =>
                (category.StartsWith("OmniSharp", StringComparison.OrdinalIgnoreCase) ||
                 string.Equals(category, typeof(ErrorHandlerMiddleware).FullName, StringComparison.OrdinalIgnoreCase)) &&
                env.TraceType <= type);

            var logger = loggerFactory.Create<Startup>();

            app.UseRequestLogging();

            app.UseErrorHandler("/error");

            app.UseMvc();

            logger.WriteInformation($"Omnisharp server running on port '{env.Port}' at location '{env.Path}' on host {env.HostPID}.");

            // Initialize everything!
            var projectSystems = app.ApplicationServices.GetRequiredService<IEnumerable<IProjectSystem>>();

            foreach (var projectSystem in projectSystems)
            {
                projectSystem.Initalize();
            }

            // Mark the workspace as initialized
            Workspace.Initialized = true;

            // This is temporary so that plugins work
            Console.WriteLine("Solution has finished loading");
        }
    }
}
