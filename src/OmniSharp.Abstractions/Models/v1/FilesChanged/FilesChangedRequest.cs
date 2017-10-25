using OmniSharp.Mef;
using System.Collections.Generic;

namespace OmniSharp.Models.FilesChanged
{
    [OmniSharpEndpoint(OmniSharpEndpoints.FilesChanged, typeof(IEnumerable<FilesChangedRequest>), typeof(FilesChangedResponse))]
    public class FilesChangedRequest : Request
    {
        public FileChangeType ChangeType { get; set; }
    }
}
