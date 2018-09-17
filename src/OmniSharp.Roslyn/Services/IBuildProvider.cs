using OmniSharp.ConfigurationManager;
using System.Composition;

namespace OmniSharp.Roslyn.Services
{
    public interface IBuildProvider
    {
        OmniSharpConfiguration config { get; set; }
    }
}
