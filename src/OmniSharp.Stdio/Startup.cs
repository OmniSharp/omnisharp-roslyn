using System;
using System.Collections.Generic;
using Microsoft.AspNet.Hosting;
using Microsoft.Framework.DependencyInjection;
using Microsoft.Framework.Logging;
using Microsoft.Framework.Runtime;
using OmniSharp.Services;
using OmniSharp.Stdio.Transport;

namespace OmniSharp.Stdio
{
    public class Startup : OmniSharp.Startup
    {
        public new void ConfigureServices(IServiceCollection services, IApplicationLifetime lifetime)
        {
            services.AddSingleton<ILoggerFactory, LoggerFactory>();
            services.AddSingleton<IApplicationShutdown, ApplicationShutdown>();
            services.AddControllers();

            base.ConfigureServices(services, lifetime);
        }

        public void Configure(IServiceProvider services,
                             ILoggerFactory loggerFactory,
                             IOmnisharpEnvironment env)
        {
            var projectSystems = services.GetRequiredService<IEnumerable<IProjectSystem>>();
            foreach (var projectSystem in projectSystems)
            {
                projectSystem.Initalize();
            }

            // Mark the workspace as initialized
            Workspace.Initialized = true;
        }
    }
}
