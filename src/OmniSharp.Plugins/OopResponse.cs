using System;
using Newtonsoft.Json.Linq;

namespace OmniSharp.Plugins
{
    class OopResponse
    {
        public static OopResponse Parse(string json)
        {
            var obj = JObject.Parse(json);
            var result = obj.ToObject<OopResponse>();

            if (result.Seq <= 0)
            {
                throw new ArgumentException("invalid seq-value");
            }

            if (string.IsNullOrWhiteSpace(result.Command))
            {
                throw new ArgumentException("missing command");
            }

            JToken arguments;
            if (obj.TryGetValue("arguments", StringComparison.OrdinalIgnoreCase, out arguments))
            {
                result.ArgumentsJson = arguments.ToString();
            }
            else
            {
                result.ArgumentsJson = string.Empty;
            }
            return result;
        }

        public int Seq { get; set; }

        public string Command { get; set; }

        public string ArgumentsJson { get; set; }

        public bool Running { get; set; }

        public bool Success { get; set; }

        public string Message { get; set; }
    }
}
