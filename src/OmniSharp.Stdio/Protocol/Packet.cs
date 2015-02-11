using Newtonsoft.Json;
using Newtonsoft.Json.Serialization;

namespace OmniSharp.Stdio.Protocol
{
    public class Packet
    {
        private readonly string _type;

        public Packet(string type)
        {
            _type = type;
        }

        public int Seq { get; set; }

        public string Type { get { return _type; } }

        public override string ToString() {
            return JsonConvert.SerializeObject(this, new JsonSerializerSettings() { ContractResolver = LowercaseContractResolver.Instance });
        }

        private class LowercaseContractResolver : DefaultContractResolver
        {
            public static DefaultContractResolver Instance = new LowercaseContractResolver();

            protected override string ResolvePropertyName(string propertyName)
            {
                return propertyName.ToLower();
            }
        }
    }
}