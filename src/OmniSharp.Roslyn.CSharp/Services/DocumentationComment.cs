using System.Text;
namespace OmniSharp.Roslyn.CSharp.Services
{
    class DocumentationComment
    {
        //[DefaultValue("")]
        public StringBuilder RemarksText { get; set; }
        public StringBuilder ExampleText { get; set; }
        public StringBuilder ExceptionText { get; set; }
        public StringBuilder ReturnsText { get; set; }
        public StringBuilder SummaryText { get; set; }
        public StringBuilder paramref { get; set; }
        public StringBuilder param { get; set; }
        public StringBuilder value { get; set; }

        

        public DocumentationComment()
        {
            RemarksText = new StringBuilder();
            ExampleText = new StringBuilder();
            ExceptionText = new StringBuilder();
            ReturnsText = new StringBuilder();
            paramref = new StringBuilder();
            param = new StringBuilder();
            value = new StringBuilder();

        }
    }
}
