using System.Collections.Immutable;
using System.Composition;
using System.Reflection;
using Microsoft.Extensions.Logging;
using OmniSharp.Services;

namespace OmniSharp.Plugins.CodeActions
{
    /// <summary>
    /// Allows us to load refactoring assemblies, such as refactoring essentials easily.
    /// </summary>
    [Export(typeof(ICodeActionProvider))]
    public class PluginCodeActionProvider : AbstractCodeActionProvider
    {
        [ImportingConstructor]
        public PluginCodeActionProvider(ILoggerFactory loggerFactory, PluginAssemblies plugins)
            : base(loggerFactory, nameof(PluginCodeActionProvider), plugins.Assemblies, throwOnException: false)
        {
            var logger = loggerFactory.CreateLogger<PluginCodeActionProvider>();

            using (logger.BeginScope("Plugin Refactorings"))
            {
                foreach (var refactoring in this.Refactorings)
                {
                    logger.LogInformation("Loaded Refactoring {0}", refactoring.GetType().FullName);
                }
            }

            using (logger.BeginScope("Plugin CodeFixes"))
            {
                foreach (var codefix in this.CodeFixes)
                {
                    logger.LogInformation("Loaded CodeFix {0}", codefix.GetType().FullName);
                }
            }
        }
    }
}
