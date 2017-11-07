using System.Collections.Generic;
using OmniSharp.FileWatching;
using OmniSharp.Mef;

namespace OmniSharp.Models.FilesChanged
{
    [OmniSharpEndpoint(OmniSharpEndpoints.FilesChanged, typeof(IEnumerable<FilesChangedRequest>), typeof(FilesChangedResponse))]
    public class FilesChangedRequest : Request
    {
        public FileChangeType ChangeType { get; set; }
    }
}
