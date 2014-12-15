using System;
using System.Collections.Generic;
using Microsoft.AspNet.Builder;
using Microsoft.AspNet.Diagnostics;
using Microsoft.AspNet.Http;
using Microsoft.Framework.DependencyInjection;
using Microsoft.Framework.Logging;
using Microsoft.Framework.Logging.Console;
using OmniSharp.AspNet5;
using OmniSharp.Services;

namespace OmniSharp
{
    public class Startup
    {
        public void ConfigureServices(IServiceCollection services)
        {
            services.AddMvc();

            // Add the omnisharp workspace to the container
            services.AddInstance(new OmnisharpWorkspace());

            // Add the initializer for ASP.NET 5 projects
            services.AddSingleton<IWorkspaceInitializer, AspNet5Initializer>();
        }

        public void Configure(IApplicationBuilder app,
                              ILoggerFactory loggerFactory,
                              IOmnisharpEnvironment env)
        {
            loggerFactory.AddConsole((category, type) => 
                (category.StartsWith("OmniSharp", StringComparison.OrdinalIgnoreCase) ||
                 string.Equals(category, typeof(ErrorHandlerMiddleware).FullName, StringComparison.OrdinalIgnoreCase)) && 
                env.TraceType <= type);
            
            app.UseErrorHandler("/error");

            app.UseMvc();

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
