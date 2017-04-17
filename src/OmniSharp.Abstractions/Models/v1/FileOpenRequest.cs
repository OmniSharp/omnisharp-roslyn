using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using OmniSharp.Mef;

namespace OmniSharp.Models
{
    [OmniSharpEndpoint(OmniSharpEndpoints.Open, typeof(FileOpenRequest), typeof(FileOpenResponse))]
    public class FileOpenRequest : Request { }
}
