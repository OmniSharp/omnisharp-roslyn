using System.Collections.Generic;
using OmniSharp.Mef;

namespace OmniSharp.Models
{
    [OmniSharpEndpoint("/filesChanged", typeof(IEnumerable<Request>), typeof(FilesChangedResponse))]
    public class FilesChangedRequest : IRequest { }
}
