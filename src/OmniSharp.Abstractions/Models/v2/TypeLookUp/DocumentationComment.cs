using System.Collections.Generic;
using System.Text;
namespace OmniSharp.Models.v2.TypeLookUp
{
    public class DocumentationComment
    {
        public string RemarksText { get; set; }
        public string ExampleText { get; set; }
        public string ReturnsText { get; set; }
        public string SummaryText { get; set; }
        public string ValueText { get; set; }
        public string [ ] ParamElements { get; set; }
        public string [ ] TypeParamElements { get; set; }
        public string [ ] Exception { get; set; }

        public DocumentationComment()
        {
            RemarksText = "";
            ExampleText = "";
            ReturnsText = "";
            SummaryText = "";
            ValueText = "";
        }
    }
}
