using System.IO;
using System.Linq;
using System.Text;
using Microsoft.AspNetCore.Server.Kestrel;
using Microsoft.AspNetCore.Server.Kestrel.Internal;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using OmniSharp.Services;
using OmniSharp.Stdio;

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
