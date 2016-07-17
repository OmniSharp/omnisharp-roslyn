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
            : base(loggerFactory, nameof(PluginCodeActionProvider), plugins.Assemblies, false)
        {
        }
    }
}
