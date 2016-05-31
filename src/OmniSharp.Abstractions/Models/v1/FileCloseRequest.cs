using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using OmniSharp.Mef;

namespace OmniSharp.Models
{
    [OmniSharpEndpoint(OmnisharpEndpoints.Close, typeof(FileCloseRequest), typeof(FileCloseResponse))]
    public class FileCloseRequest : Request { }
}
