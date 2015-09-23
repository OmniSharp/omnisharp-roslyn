using System.Collections.Generic;
using OmniSharp.Mef;

namespace OmniSharp.Models
{
    [OmniSharpEndpoint("/filesChanged", typeof(IEnumerable<Request>), typeof(object), TakeOne = true)]
    public class FilesChangedRequest : IRequest { }
}
