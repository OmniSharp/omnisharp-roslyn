using System.Collections.Generic;

namespace OmniSharp.Abstractions.Models.V1.FixAll
{
    public class GetFixAllResponse
    {
        public GetFixAllResponse(IEnumerable<FixAllItem> fixableItems)
        {
            Items = fixableItems;
        }

        public IEnumerable<FixAllItem> Items { get; set; }
    }
}