using System.Composition;
using OmniSharp.Mef;
using OmniSharp.Models.FileOpen;

namespace OmniSharp.Cake.Services.RequestHandlers.Files;

[OmniSharpHandler(OmniSharpEndpoints.Open, Constants.LanguageNames.Cake), Shared]
public class FileOpenHandler : CakeRequestHandler<FileOpenRequest, FileOpenResponse>
{
    [ImportingConstructor]
    public FileOpenHandler(OmniSharpWorkspace workspace) : base(workspace)
    {
    }
}
