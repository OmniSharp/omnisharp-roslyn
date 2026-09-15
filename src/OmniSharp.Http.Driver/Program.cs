using System;
using OmniSharp.Plugins;
using OmniSharp.Services;
using OmniSharp.Utilities;

namespace OmniSharp.Http.Driver
{
    internal class Program
    {
        static int Main(string[] args)
        {
            WindowsHandleInheritance.DisableStandardHandleInheritance();
            return HostHelpers.Start(() =>
            {
                var application = new HttpCommandLineApplication();
                application.OnExecute(() =>
                {
                    var environment = application.CreateEnvironment();
                    Configuration.ZeroBasedIndices = application.ZeroBasedIndices;

                    var writer = new SharedTextWriter(Console.Out);
                    var commandLinePlugins = new PluginAssemblies(application.Plugin);

                    var host = new Host(environment, writer, commandLinePlugins, application.Port, application.Interface);
                    host.Start();

                    return 0;
                });

                return application.Execute(args);
            });
        }

    }
}
