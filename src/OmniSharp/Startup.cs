using System;
using System.Collections.Generic;
using System.IO;
using Microsoft.AspNet.Builder;
using Microsoft.AspNet.Diagnostics;
using Microsoft.AspNet.Mvc;
using Microsoft.Framework.Caching.Memory;
using Microsoft.Framework.ConfigurationModel;
using Microsoft.Framework.DependencyInjection;
using Microsoft.Framework.Logging;
using OmniSharp.AspNet5;
using OmniSharp.Filters;
using OmniSharp.Middleware;
using OmniSharp.MSBuild;
using OmniSharp.Options;
using OmniSharp.Roslyn;
using OmniSharp.Services;
using OmniSharp.Settings;
using OmniSharp.Stdio.Logging;
using OmniSharp.Stdio.Services;

namespace OmniSharp
{
    public class Startup
    {
        public Startup()
        {
           var configuration = new Configuration()
                .AddJsonFile("config.json");

            if (Program.Environment.OtherArgs != null)
            {
                configuration.AddCommandLine(Program.Environment.OtherArgs);
            }

            // Use the local omnisharp config if there's any in the root path
            if (File.Exists(Program.Environment.ConfigurationPath))
            {
                configuration.AddJsonFile(Program.Environment.ConfigurationPath);
            }
            configuration.AddEnvironmentVariables();

            Configuration = configuration;
        }

        public IConfiguration Configuration { get; private set; }

        public OmnisharpWorkspace Workspace { get; set; }

        public void ConfigureServices(IServiceCollection services)
        {
            Workspace = new OmnisharpWorkspace();

            services.AddMvc();

            services.Configure<MvcOptions>(opt =>
            {
                opt.Conventions.Add(new FromBodyApplicationModelConvention());
                opt.Filters.Add(new UpdateBufferFilter(Workspace));
            });

            // Add the omnisharp workspace to the container
            services.AddInstance(Workspace);

            // Caching
            services.AddSingleton<IMemoryCache, MemoryCache>();
            services.AddSingleton<IMetadataFileReferenceCache, MetadataFileReferenceCache>();

            // Add the project systems
            services.AddInstance(new AspNet5Context());
            services.AddInstance(new MSBuildContext());
            services.AddInstance(new ScriptCs.ScriptCsContext());

            services.AddSingleton<IProjectSystem, AspNet5ProjectSystem>();
            services.AddSingleton<IProjectSystem, MSBuildProjectSystem>();

#if DNX451
            services.AddSingleton<IProjectSystem, ScriptCs.ScriptCsProjectSystem>();
#endif

            // Add the file watcher
            services.AddSingleton<IFileSystemWatcher, ManualFileSystemWatcher>();

            // Add test command providers
            services.AddSingleton<ITestCommandProvider, AspNet5TestCommandProvider>();

            // Add the code action provider
            services.AddSingleton<ICodeActionProvider, EmptyCodeActionProvider>();

#if DNX451
            //TODO Do roslyn code actions run on Core CLR?
            services.AddSingleton<ICodeActionProvider, RoslynCodeActionProvider>();
            services.AddSingleton<ICodeActionProvider, NRefactoryCodeActionProvider>();
#endif

            if (Program.Environment.TransportType == TransportType.Stdio)
            {
                services.AddSingleton<IEventEmitter, StdioEventEmitter>();
            }
            else
            {
                services.AddSingleton<IEventEmitter, NullEventEmitter>();
            }

            services.AddSingleton<ProjectEventForwarder, ProjectEventForwarder>();

            // Setup the options from configuration
            services.Configure<OmniSharpOptions>(Configuration);
        }

        public void Configure(IApplicationBuilder app,
                              ILoggerFactory loggerFactory,
                              IOmnisharpEnvironment env,
                              ISharedTextWriter writer)
        {
            Func<string, LogLevel, bool> logFilter = (category, type) =>
                (category.StartsWith("OmniSharp", StringComparison.OrdinalIgnoreCase) || string.Equals(category, typeof(ErrorHandlerMiddleware).FullName, StringComparison.OrdinalIgnoreCase))
                && env.TraceType <= type;

            if (env.TransportType == TransportType.Stdio)
            {
                loggerFactory.AddStdio(writer, logFilter);
            }
            else
            {
                loggerFactory.AddConsole(logFilter);
            }

            var logger = loggerFactory.CreateLogger<Startup>();

            app.UseRequestLogging();

            app.UseErrorHandler("/error");

            app.UseMvc();

            if (env.TransportType == TransportType.Stdio)
            {
                logger.LogInformation($"Omnisharp server running using stdio at location '{env.Path}' on host {env.HostPID}.");
            }
            else
            {
                logger.LogInformation($"Omnisharp server running on port '{env.Port}' at location '{env.Path}' on host {env.HostPID}.");
            }

            // Forward workspace events
            app.ApplicationServices.GetRequiredService<ProjectEventForwarder>();

            // Initialize everything!
            var projectSystems = app.ApplicationServices.GetRequiredServices<IProjectSystem>();

            foreach (var projectSystem in projectSystems)
            {
                projectSystem.Initalize();
            }

            // Mark the workspace as initialized
            Workspace.Initialized = true;

            logger.LogInformation("Solution has finished loading");
        }
    }
}
