using System;
using System.Collections.Generic;
using OmniSharp.Mef;

namespace OmniSharp.Models
{
    public class FilesChangedResponse : IMergeableResponse
    {
        public IMergeableResponse Merge(IMergeableResponse response)
        {
            // File Changes just need to inform plugins, editors are not expecting any kind of real response
            return response ?? new FilesChangedResponse();
        }
    }
}
