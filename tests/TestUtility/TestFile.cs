namespace TestUtility
{
    public class TestFile
    {
        public string FileName { get; }
        public TestContent Content { get; }

        public TestFile(string fileName, TestContent content)
        {
            this.FileName = fileName;
            this.Content = content;
        }

        public TestFile(string fileName, string content)
            : this(fileName, TestContent.Parse(content))
        {
        }
    }
}
