using System.Collections.Generic;
using Newtonsoft.Json;
using OmniSharp.Mef;

namespace OmniSharp.Models.FilesChanged
{
    [OmniSharpEndpoint(OmniSharpEndpoints.FilesChanged, typeof(IEnumerable<FilesChangedRequest>), typeof(FilesChangedResponse))]
    public class FilesChangedRequest : Request
    {
        //[JsonConverter(typeof(FileChangeTypeConverter))]
        public FileChangeType ChangeType { get; set; }
    }
}
