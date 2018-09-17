using OmniSharp.ConfigurationManager;
using OmniSharp.Roslyn.Services;
using System.Composition;

namespace OmniSharp.Roslyn.CSharp.Services
{
    [Export(typeof(IBuildProvider))]
    class BuildProvider : IBuildProvider
    {
        public OmniSharpConfiguration config { get => ConfigurationLoader.Config; set => throw new System.NotImplementedException(); }
    }
}
