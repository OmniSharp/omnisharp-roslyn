using System.Composition;
using OmniSharp.Services;

namespace OmniSharp.Roslyn.CSharp.Services.CodeActions
{
    [Export(typeof(ICodeActionProvider))]
    public class RoslynCodeActionProvider : AbstractCodeActionProvider
    {
        // TODO: Come in and pass Microsoft.CodeAnalysis.Features as well (today this breaks)
        [ImportingConstructor]
        public RoslynCodeActionProvider(IOmnisharpAssemblyLoader loader)
            : base("Roslyn", loader,
                  "Microsoft.CodeAnalysis.CSharp.Features, Version=1.1.0.0, Culture=neutral, PublicKeyToken=31bf3856ad364e35",
                  "Microsoft.CodeAnalysis.Features, Version=1.1.0.0, Culture=neutral, PublicKeyToken=31bf3856ad364e35")
        { }
    }
}
