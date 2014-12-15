using System;
using System.Collections.Generic;
using Microsoft.AspNet.Builder;
using Microsoft.AspNet.Diagnostics;
using Microsoft.AspNet.Http;
using Microsoft.Framework.Cache.Memory;
using Microsoft.Framework.ConfigurationModel;
using Microsoft.Framework.DependencyInjection;
using Microsoft.Framework.Logging;
using Microsoft.Framework.Logging.Console;
using OmniSharp.AspNet5;
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

        public void ConfigureServices(IServiceCollection services)
        {
            services.AddMvc(Configuration);

            // Add the omnisharp workspace to the container
            services.AddInstance(new OmnisharpWorkspace());

            // Caching
            services.AddSingleton<IMemoryCache, MemoryCache>();
            services.AddSingleton<IMetadataFileReferenceCache, MetadataFileReferenceCache>();

            // Add the initializer for ASP.NET 5 projects
            services.AddSingleton<IWorkspaceInitializer, AspNet5Initializer>();

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
            
            app.UseErrorHandler("/error");

            app.UseMvc();

            logger.WriteInformation(string.Format("Omnisharp server running on port '{0}' at location '{1}'.", env.Port, env.Path));

            // Initialize everything!
            var initializers = app.ApplicationServices.GetRequiredService<IEnumerable<IWorkspaceInitializer>>();

            foreach (var initializer in initializers)
            {
                initializer.Initalize();
            }

            // This is temporary so that plugins work
            Console.WriteLine("Solution has finished loading");
        }
    }
}
