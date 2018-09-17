using System.IO;
using System.Reflection;
using System.Text.RegularExpressions;

namespace OmniSharp.ConfigurationManager
{
    public class ConfigurationLoader
    {
        private static OmniSharpConfiguration _config = new OmniSharpConfiguration();

        public static OmniSharpConfiguration Load(string configLocation)
        {
            if (string.IsNullOrEmpty(configLocation))
            {
                string executableLocation = Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location);
                configLocation = Path.Combine(executableLocation, "config.json");
            }
            var config = StripComments(File.ReadAllText(configLocation));
            _config = new Nancy.Json.JavaScriptSerializer().Deserialize<OmniSharpConfiguration>(config);
            _config.ConfigFileLocation = configLocation;

            return _config;
        }

        private static string StripComments(string json)
        {
            const string pattern = @"/\*(?>(?:(?>[^*]+)|\*(?!/))*)\*/";

            return Regex.Replace(json, pattern, string.Empty, RegexOptions.Multiline);    
        }

        public static OmniSharpConfiguration Config { get { return _config; }}
    }
}
