using System.IO;
using System.Text;
using OmniSharp.Stdio.Protocol;
using OmniSharp.Stdio.Services;
using Xunit;

namespace OmniSharp.Stdio.Tests
{
    public class StdioResponseStreamFacts
    {
        [Fact]
        public void SharedWriterOnlyUsedWithFirstWrite()
        {
            var buffer = new StringBuilder();
            using (var writer = new StringWriter(buffer))
            {
                writer.NewLine = "\n";
                var sharedWriter = new SharedTextWriter(writer);
                using (var stream = new StdioResponseStream(sharedWriter, new ResponsePacket() { Seq = 1, Command = "foo" }))
                {
                    sharedWriter.Use(w => w.WriteLine("bar")); // this comes first

                    var data = Encoding.UTF8.GetBytes("{}");
                    stream.Write(data, 0, data.Length);
                }
            }
            Assert.True(buffer.ToString().IndexOf("bar\n{") == 0);
        }

        [Fact]
        public void BodyIsWrittenFirst()
        {
            var buffer = new StringBuilder();
            using (var writer = new StringWriter(buffer))
            {
                writer.NewLine = "\n";
                var sharedWriter = new SharedTextWriter(writer);
                using (var stream = new StdioResponseStream(sharedWriter, new ResponsePacket() { Seq = 1, Command = "foo", Success = true, Running = true }))
                {
                    var data = Encoding.UTF8.GetBytes("{}");
                    stream.Write(data, 0, data.Length);
                }
            }
            var expected = "{\"body\":{},\"seq\":1,\"request_seq\":0,\"type\":\"response\",\"command\":\"foo\",\"running\":true,\"success\":true,\"message\":null}\n";
            Assert.Equal(expected, buffer.ToString());
        }
    }
}
