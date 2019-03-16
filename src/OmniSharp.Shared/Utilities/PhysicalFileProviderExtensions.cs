using Microsoft.Extensions.FileProviders;

namespace OmniSharp.Utilities
{
    public static partial class PhysicalFileProviderExtensions
    {
        /// <summary>
        /// Wraps a PhysicalFileProvider with an IFileProvider that handles polling files for for changes
        /// rather than using FileSystemWatcher.
        /// </summary>
        public static IFileProvider WrapForPolling(this PhysicalFileProvider fileProvider)
        {
            return new PhysicalFileProviderWrapper(fileProvider);
        }
    }
}
