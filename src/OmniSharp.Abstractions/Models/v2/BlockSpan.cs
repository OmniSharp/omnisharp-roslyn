using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace OmniSharp.Models.V2
{
    public class BlockSpan
    {
        public BlockSpan(Range textSpan, Range hintSpan, string bannerText,  string type)
        {
            TextSpan = textSpan;
            HintSpan = hintSpan;
            BannerText = bannerText;
            Type = type;
        }

        /// <summary>
        /// The span of text to collapse.
        /// </summary>
        public Range TextSpan { get; }

        /// <summary>
        /// The span of text to display in the hint on mouse hover.
        /// </summary>
        public Range HintSpan { get; }

        /// <summary>
        /// The text to display inside the collapsed region.
        /// </summary>
        public string BannerText { get; }

        public string Type { get; }
    }
}
