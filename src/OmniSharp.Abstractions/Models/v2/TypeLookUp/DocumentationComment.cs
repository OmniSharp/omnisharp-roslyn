using System.Collections.Generic;
using System.Text;
namespace OmniSharp.Models.v2.TypeLookUp
{
    public class DocumentationComment
    {
        //[DefaultValue("")]
        public StringBuilder RemarksText { get; set; }
        public StringBuilder ExampleText { get; set; }
        public StringBuilder ExceptionText { get; set; }
        public StringBuilder ReturnsText { get; set; }
        public StringBuilder SummaryText { get; set; }
        public StringBuilder ValueText { get; set; }
        public StringBuilder paramref { get; set; }
        public List<StringBuilder> Param { get; set; }
        public List<StringBuilder> TypeParam { get; set; }

        public DocumentationComment()
        {
            RemarksText = new StringBuilder();
            ExampleText = new StringBuilder();
            ExceptionText = new StringBuilder();
            ReturnsText = new StringBuilder();
            SummaryText = new StringBuilder();
            ValueText = new StringBuilder();
            paramref = new StringBuilder();
            Param = new List<StringBuilder>();
            TypeParam = new List<StringBuilder>();

        }
    }
}
