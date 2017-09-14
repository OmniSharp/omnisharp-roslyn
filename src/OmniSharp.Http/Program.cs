using OmniSharp.Services;

namespace OmniSharp.Http
{
    class Program
    {
        static int Main(string[] args) => HostHelpers.Start(() =>
        {
            var application = new HttpCommandLineApplication();
            application.OnExecute(() =>
            {
                var environment = application.CreateEnvironment();
                var writer = new SharedConsoleWriter();
                var plugins = application.CreatePluginAssemblies();

                var host = new Host(environment, writer, plugins, application.Port, application.Interface);
                host.Start();

                return 0;
            });

            return application.Execute(args);
        });

    }
}
