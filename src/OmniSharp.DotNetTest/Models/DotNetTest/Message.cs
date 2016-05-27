using Newtonsoft.Json.Linq;

namespace OmniSharp.DotNetTest.Models.DotNetTest
{
    internal class Message<T>
    {
        public string MessageType { get; set; }

        public T Payload { get; set; }
    }

    internal class Message : Message<JToken>
    {
    }
}
