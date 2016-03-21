using System;
using System.Collections.Generic;
using System.IO;
using Newtonsoft.Json;

namespace OmniSharp.Tools.PublishProject
{
    public class BuildPlan
    {
        public static BuildPlan Parse(string root)
        {
            try
            {
                var content = File.ReadAllText(Path.Combine(root, "build.json"));
                var result = JsonConvert.DeserializeObject<BuildPlan>(content);
                result.Root = root;

                return result;
            }
            catch (System.Exception ex)
            {
                Console.Error.WriteLine(ex.Message);
                throw;
            }
        }

        public IDictionary<string, string[]> TestProjects { get; set; }

        public string BuildToolsFolder { get; set; }

        public string ArtifactsFolder { get; set; }

        public string DotNetFolder { get; set; }

        public string[] Frameworks { get; set; }

        public string[] Rids { get; set; }

        public string MainProject { get; set; }

        [JsonIgnore]
        public string Root { get; set; }
    }
}