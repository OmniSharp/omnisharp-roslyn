using System.Collections.Generic;
using System.Linq;

namespace OmniSharp.Models.BuildCommand
{
    public class BuildCommandResponse : IAggregateResponse
    {
        //public string Command { get; set; }

        #region IAggregateResponse

        public IEnumerable<QuickFix> QuickFixes { get; set; }
        
        public IAggregateResponse Merge(IAggregateResponse response)
        {
            var quickFixResponse = (QuickFixResponse)response;
            return new QuickFixResponse(this.QuickFixes.Concat(quickFixResponse.QuickFixes));
        }

        #endregion IAggregateResponse
    }
}
