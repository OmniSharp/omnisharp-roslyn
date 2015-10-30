using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Threading.Tasks;
using Microsoft.Framework.Runtime;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace OmniSharp.Bootstrap
{
    public class Program
    {
        private readonly IApplicationEnvironment _appEnv;
        private readonly string[] _nonPlugins = { "OmniSharp.Plugins", "OmniSharp.Stdio", "OmniSharp", "OmniSharp.Stdio" };

        public Program(IApplicationEnvironment appEnv)
        {
            _appEnv = appEnv;
        }

        public void Main(string[] args)
        {
            var enumerator = args.GetEnumerator();
            var pluginPaths = new List<string>();
            var bootstrapPath = Path.GetDirectoryName(_appEnv.ApplicationBasePath);
            var omnisharpProjectPath = Path.Combine(bootstrapPath, "OmniSharp", "project.json");
            if (!File.Exists(omnisharpProjectPath)) {
                omnisharpProjectPath = Path.Combine(bootstrapPath, "OmniSharp", "1.0.0", "root", "project.json");
            }
            var defaultFrameworks = JObject.Parse(File.ReadAllText(omnisharpProjectPath))["frameworks"]
                .Select(x => x.Path.Replace("frameworks.", ""))
                .OrderBy(x => x).ToArray();

            pluginPaths.Add(bootstrapPath);

            while (enumerator.MoveNext())
            {
                var arg = (string)enumerator.Current;

                if (arg == "--plugin")
                {
                    enumerator.MoveNext();
                    pluginPaths.Add((string)enumerator.Current);
                }
            }

            // Find a repeatable user based location
            var home = new string[] { Environment.GetEnvironmentVariable("HOME"), Environment.GetEnvironmentVariable("USERPROFILE") }.Where(s => !string.IsNullOrEmpty(s)).First();
            var omnisharpHome = Path.Combine(home, ".omnisharp");
            if (!Directory.Exists(omnisharpHome))
            {
                Directory.CreateDirectory(omnisharpHome);
            }

            var md5 = MD5.Create();

            var pluginAges = new Dictionary<string, string>();
            var offset = 0;

            // OrderBy ensures consistent hashing
            foreach (var path in pluginPaths.OrderBy(x => x))
            {
                var bytes = System.Text.Encoding.UTF8.GetBytes(path);
                md5.TransformBlock(bytes, offset, bytes.Length, null, 0);

                var projectJsonFiles = Directory.EnumerateFiles(path, "project.json", SearchOption.AllDirectories).OrderBy(x => x);
                foreach (var projectJson in projectJsonFiles)
                {
                    var writeTime = File.GetLastWriteTime(projectJson);
                    bytes = System.Text.Encoding.UTF8.GetBytes(writeTime.Ticks.ToString());
                    md5.TransformBlock(bytes, offset, bytes.Length, null, 0);
                }

                var omnisharpPlugins = Directory.EnumerateFiles(path, "omnisharp.plugin", SearchOption.AllDirectories).OrderBy(x => x);
                foreach (var omnisharpPlugin in omnisharpPlugins)
                {
                    var writeTime = File.GetLastWriteTime(omnisharpPlugin);
                    bytes = System.Text.Encoding.UTF8.GetBytes(writeTime.Ticks.ToString());
                    md5.TransformBlock(bytes, offset, bytes.Length, null, 0);
                }
            }

            md5.TransformFinalBlock(new byte[0], 0, 0);

            var hash = string.Join("", Convert.ToBase64String(md5.Hash).Except(Path.GetInvalidFileNameChars()));
            Console.WriteLine(hash);
            var omnisharpPath = Path.Combine(omnisharpHome, hash);
            // if (Directory.Exists(omnisharpPath))
            // {
            //     Console.Write(Path.Combine(omnisharpPath, "bootstrap/OmniSharp.WithPlugins"));
            //     return;
            // }

            Directory.CreateDirectory(omnisharpPath);
            var globalJobject = new JObject();
            globalJobject["projects"] = new JArray(new string[] { "bootstrap" }.Concat(pluginPaths).ToArray());

            var sdkJobject = new JObject();
            sdkJobject["version"] = new JValue("1.0.0-beta4");

            globalJobject["sdk"] = sdkJobject;

            File.WriteAllText(Path.Combine(omnisharpPath, "global.json"), globalJobject.ToString());

            Directory.CreateDirectory(Path.Combine(omnisharpPath, "bootstrap"));
            Directory.CreateDirectory(Path.Combine(omnisharpPath, "bootstrap", "OmniSharp.WithPlugins"));

            var pluginDirectories = pluginPaths
                .SelectMany(pluginPath => Directory.EnumerateDirectories(pluginPath)
                    .Where(directory => File.Exists(Path.Combine(directory, "project.json")) || File.Exists(Path.Combine(directory, "1.0.0", "root", "project.json"))))
                    .ToArray();

            var allDeps = new Dictionary<string, string>();
            allDeps.Add("OmniSharp", "1.0.0-*");
            var frameworkDeps = new Dictionary<string, Dictionary<string, string>>();
            foreach (var framework in defaultFrameworks)
            {
                frameworkDeps.Add(framework, new Dictionary<string, string>());
            }

            foreach (var dir in pluginDirectories)
            {
                JObject json = null;
                var path = Path.Combine(dir, "project.json");
                if (!File.Exists(path))
                {
                    path = Path.Combine("dir", "1.0.0", "root", "project.json");
                }

                try
                {
                    json = JObject.Parse(File.ReadAllText(path));
                }
                catch { }

                if (json != null)
                {
                    var name = Path.GetFileName(dir);
                    Console.WriteLine(name);

                    var frameworks = json["frameworks"].Select(x => x.Path.Replace("frameworks.", "")).OrderBy(x => x).ToArray();

                    if (defaultFrameworks.SequenceEqual(frameworks))
                    {
                        if (allDeps.ContainsKey(name))
                        {
                            Console.WriteLine($"name found {name}");
                        }
                        else
                        {
                            allDeps.Add(name, "1.0.0-*");
                        }
                    }
                    else
                    {
                        foreach (var framework in frameworks)
                        {
                            if (defaultFrameworks.Contains(framework))
                            {
                                Dictionary<string, string> deps = null;
                                if (frameworkDeps.TryGetValue(framework, out deps))
                                {
                                    if (deps.ContainsKey(name))
                                    {
                                        Console.WriteLine($"name found {name}");
                                    }
                                    else
                                    {
                                        deps.Add(name, "1.0.0-*");
                                    }
                                }
                            }
                        }
                    }
                }
            }

            var projectJobject = new JObject();
            projectJobject["version"] = new JValue("1.0.0-*");
            projectJobject["dependencies"] = JObject.FromObject(allDeps);
            projectJobject["commands"] = JObject.FromObject(new Dictionary<string, string> {
                { "OmniSharp.Bootstrapper", "OmniSharp.Bootstrapper" }
                });

            var frameworksJobject = new JObject();
            foreach (var deps in frameworkDeps)
            {
                frameworksJobject[deps.Key] = JObject.FromObject(deps.Value);
            }

            projectJobject["frameworks"] = frameworksJobject;
            projectJobject["entryPoint"] = new JValue("OmniSharp");

            File.WriteAllText(Path.Combine(omnisharpPath, "bootstrap", "OmniSharp.WithPlugins", "project.json"), projectJobject.ToString());
            File.Copy(Path.Combine(Path.GetDirectoryName(omnisharpProjectPath), "config.json"), Path.Combine(omnisharpPath, "bootstrap", "OmniSharp.WithPlugins", "config.json"));

            // Scaffold out an app that uses OmniSharp, has a global.json that references all the Plugins that we want to load.
            // Put that in a temporary directory
            // return the full Path to the folder that will Run omnisharp

        }
    }
}
