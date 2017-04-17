using System.Collections.Generic;
using OmniSharp.Mef;

namespace OmniSharp.Models
{
    [OmniSharpEndpoint(OmniSharpEndpoints.FilesChanged, typeof(IEnumerable<Request>), typeof(FilesChangedResponse))]
    public class FilesChangedRequest : IRequest { }
}
