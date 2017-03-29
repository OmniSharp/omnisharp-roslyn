using Newtonsoft.Json.Linq;

namespace OmniSharp.DotNetTest.Models.DotNetTest
{
    public class Message<T>
    {
        public string MessageType { get; set; }

        public T Payload { get; set; }
    }
}
