using System;
using System.Collections.Generic;
using System.Composition.Hosting;
using System.IO;
using System.Linq;
using System.Reflection;
using Microsoft.AspNet.Builder;
using Microsoft.AspNet.Diagnostics;
using Microsoft.AspNet.Hosting;
using Microsoft.AspNet.Mvc;
using Microsoft.CodeAnalysis.Host.Mef;
using Microsoft.Framework.Caching.Memory;
using Microsoft.Framework.ConfigurationModel;
using Microsoft.Framework.DependencyInjection;
using Microsoft.Framework.Logging;
using Microsoft.Framework.OptionsModel;
using Microsoft.Framework.Runtime;
using OmniSharp.Dnx;
using OmniSharp.Filters;
using OmniSharp.Mef;
using OmniSharp.Middleware;
using OmniSharp.Options;
using OmniSharp.Plugins;
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

        public CompositionHost PluginHost { get; private set; }

        public void ConfigureServices(IServiceCollection services)
        {
            Workspace = CreateWorkspace();
            services.AddMvc();

            services.Configure<MvcOptions>(opt =>
            {
                opt.Conventions.Add(new FromBodyApplicationModelConvention());
                opt.Filters.Add(new UpdateBufferFilter(Workspace));
            });

            // Add the omnisharp workspace to the container
            services.AddInstance(Workspace);
            services.AddSingleton(typeof(CompositionHost), (x) => PluginHost);

            // Caching
            services.AddSingleton<IMemoryCache, MemoryCache>();
            services.AddSingleton<IMetadataFileReferenceCache, MetadataFileReferenceCache>();

            // Add the file watcher
            services.AddSingleton<IFileSystemWatcher, ManualFileSystemWatcher>();

#if DNX451
            //TODO Do roslyn code actions run on Core CLR?
            services.AddSingleton<ICodeActionProvider, RoslynCodeActionProvider>();
            services.AddSingleton<ICodeActionProvider, NRefactoryCodeActionProvider>();
#endif

            foreach (var endpoint in Endpoints.AvailableEndpoints)
            {
                services.AddInstance(endpoint);
            }

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

        public static OmnisharpWorkspace CreateWorkspace()
        {
            var assemblies = MefHostServices.DefaultAssemblies;
#if DNX451
            assemblies = assemblies.AddRange(RoslynCodeActionProvider.MefAssemblies);
            assemblies = assemblies.AddRange(NRefactoryCodeActionProvider.MefAssemblies);
#endif
            return new OmnisharpWorkspace(MefHostServices.Create(assemblies));
        }

        public static CompositionHost ConfigurePluginHost(IServiceProvider serviceProvider,
                                                          OmnisharpWorkspace workspace,
                                                          ILoggerFactory loggerFactory,
                                                          IOmnisharpEnvironment env,
                                                          ISharedTextWriter writer,
                                                          OmniSharpOptions options,
                                                          IMetadataFileReferenceCache metadataFileReferenceCache,
                                                          IApplicationLifetime applicationLifetime,
                                                          IFileSystemWatcher fileSystemWatcher,
                                                          IEventEmitter eventEmitter,
                                                          IEnumerable<Assembly> assemblies,
                                                          Func<ContainerConfiguration, ContainerConfiguration> configure = null)
        {
            var config = new ContainerConfiguration();
            foreach (var assembly in assemblies)
            {
                config = config.WithAssembly(assembly);
            }

            //IOmnisharpEnvironment env, ILoggerFactory loggerFactory
            config = config.WithProvider(MefValueProvider.From(workspace))
                .WithProvider(MefValueProvider.From(serviceProvider))
                .WithProvider(MefValueProvider.From(loggerFactory))
                .WithProvider(MefValueProvider.From(env))
                .WithProvider(MefValueProvider.From(writer))
                .WithProvider(MefValueProvider.From(options))
                .WithProvider(MefValueProvider.From(options.FormattingOptions))
                .WithProvider(MefValueProvider.From(metadataFileReferenceCache))
                .WithProvider(MefValueProvider.From(applicationLifetime))
                .WithProvider(MefValueProvider.From(fileSystemWatcher))
                .WithProvider(MefValueProvider.From(eventEmitter));

            if (configure != null)
                config = configure(config);

            return config.CreateContainer();
        }

        public void Configure(IApplicationBuilder app,
                              ILoggerFactory loggerFactory,
                              IOmnisharpEnvironment env,
                              ISharedTextWriter writer,
                              IServiceProvider serviceProvider,
                              ILibraryManager manager,
                              IOptions<OmniSharpOptions> optionsAccessor,
                              IMetadataFileReferenceCache metadataFileReferenceCache,
                              IApplicationLifetime applicationLifetime,
                              IFileSystemWatcher fileSystemWatcher,
                              IEventEmitter eventEmitter,
                              PluginAssemblies plugins)
        {
            if (plugins.AssemblyNames.Any())
            {
                PluginHost = ConfigurePluginHost(serviceProvider, Workspace, loggerFactory, env, writer, optionsAccessor.Options, metadataFileReferenceCache, applicationLifetime, fileSystemWatcher, eventEmitter, manager.GetLibraries()
                    .SelectMany(x => x.LoadableAssemblies)
                    .Join(plugins.AssemblyNames, x => x.FullName, x => x, (library, name) => library)
                    .Select(assemblyName => Assembly.Load(assemblyName)));
            }
            else
            {
                PluginHost = ConfigurePluginHost(serviceProvider, Workspace, loggerFactory, env, writer, optionsAccessor.Options, metadataFileReferenceCache, applicationLifetime, fileSystemWatcher, eventEmitter, manager.GetReferencingLibraries("OmniSharp.Abstractions")
                    .SelectMany(libraryInformation => libraryInformation.LoadableAssemblies)
                    .Select(assemblyName => Assembly.Load(assemblyName)));
            }

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

            // TODO: When we wire up plugins, we may need to hand them off to this middleware too.
            app.UseMiddleware<StatusMiddleware>();
            app.UseMiddleware<ProjectSystemMiddleware>();
            app.UseMiddleware<EndpointMiddleware>();
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
            foreach (var projectSystem in PluginHost.GetExports<IProjectSystem>())
            {
                try
                {
                    projectSystem.Initalize(Configuration.GetSubKey(projectSystem.Key));
                }
                catch (Exception e)
                {
                    //if a project system throws an unhandled exception
                    //it should not crash the entire server
                    logger.LogError($"The project system '{projectSystem.GetType().Name}' threw an exception.", e);
                }
            }

            // Mark the workspace as initialized
            Workspace.Initialized = true;

            logger.LogInformation("Solution has finished loading");
        }
    }
}
