using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using Microsoft.Extensions.PlatformAbstractions;
using Newtonsoft.Json.Linq;

namespace OmniSharp.Bootstrap
{
    public class Program
    {
        private readonly IApplicationEnvironment _appEnv;
        private readonly string[] _nonPlugins = { "OmniSharp.Plugins", "OmniSharp.Abstractions", "OmniSharp.Stdio", "OmniSharp", "OmniSharp.Host" };

        public Program(IApplicationEnvironment appEnv)
        {
            _appEnv = appEnv;
        }

        public string OmnisharpPath { get; set; }
        public List<string> PluginPaths { get; set; } = new List<string>();
        public List<KeyValuePair<string, string>> PluginNames { get; set; } = new List<KeyValuePair<string, string>>();
        public string SolutionRoot { get; set; } = Directory.GetCurrentDirectory();
        public string BootstrapPath { get; set; }
        public string OmnisharpProjectPath { get; set; }

        public void ParseArguments(string[] args)
        {
            var enumerator = args.GetEnumerator();

            while (enumerator.MoveNext())
            {
                var arg = (string)enumerator.Current;

                if (arg == "--plugins")
                {
                    enumerator.MoveNext();
                    PluginPaths.Add((string)enumerator.Current);
                }

                if (arg == "--plugin-name")
                {
                    enumerator.MoveNext();
                    var v = (string)enumerator.Current;
                    var s = v.Split('@');
                    if (s.Length > 1)
                    {
                        PluginNames.Add(new KeyValuePair<string, string>(s[0], s[1]));
                    }
                    else
                    {
                        PluginNames.Add(new KeyValuePair<string, string>(s[0], string.Empty));
                    }
                }

                if (arg == "-s")
                {
                    enumerator.MoveNext();
                    SolutionRoot = Path.GetFullPath((string)enumerator.Current);
                }
            }

            BootstrapPath = Path.GetDirectoryName(_appEnv.ApplicationBasePath);
            OmnisharpProjectPath = Path.Combine(BootstrapPath, "OmniSharp.Host", "project.json");
            if (!File.Exists(OmnisharpProjectPath))
            {
                OmnisharpProjectPath = Path.Combine(OmnisharpProjectPath, "OmniSharp.Host", "1.0.0", "root", "project.json");
            }

            if (!string.IsNullOrEmpty(SolutionRoot))
            {
                var pluginsFolder = Path.Combine(SolutionRoot, ".omnisharp", "plugins");
                if (Directory.Exists(pluginsFolder))
                {
                    PluginPaths.Add(pluginsFolder);
                }

                var omnisharpJsonPath = Path.Combine(SolutionRoot, "omnisharp.json");
                if (File.Exists(omnisharpJsonPath))
                {
                    var omnisharpJson = JObject.Parse(File.ReadAllText(omnisharpJsonPath));
                    if (omnisharpJson["plugins"] != null)
                    {
                        var omnisharpJsonPlugins = omnisharpJson["plugins"];
                        foreach (var plugin in omnisharpJsonPlugins)
                        {
                            if (plugin is JObject)
                            {
                                var pluginJobject = plugin as JObject;
                                PluginNames.Add(new KeyValuePair<string, string>(pluginJobject["name"].ToString(), pluginJobject["version"].ToString()));
                            }
                            else if (plugin is JToken)
                            {
                                var pluginString = plugin.ToString();
                                var pluginSplitString = pluginString.Split('@');
                                if (pluginSplitString.Length > 1)
                                {
                                    PluginNames.Add(new KeyValuePair<string, string>(pluginSplitString[0], pluginSplitString[1]));
                                }
                                else
                                {
                                    PluginNames.Add(new KeyValuePair<string, string>(pluginSplitString[0], string.Empty));
                                }
                            }
                        }
                    }
                }
            }
        }

        public int Main(string[] args)
        {
            ParseArguments(args);

            if (!PluginPaths.Any() && !PluginNames.Any())
            {
                Console.Write(Path.GetDirectoryName(OmnisharpProjectPath));
                return 0;
            }

            var defaultFrameworks = JObject.Parse(File.ReadAllText(OmnisharpProjectPath))["frameworks"]
                .Select(x => x.Path.Replace("frameworks.", ""))
                .OrderBy(x => x).ToArray();

            // Find a repeatable user based location
            var home = new string[] { Environment.GetEnvironmentVariable("HOME"), Environment.GetEnvironmentVariable("USERPROFILE") }.Where(s => !string.IsNullOrEmpty(s)).First();
            var omnisharpHome = Path.Combine(home, ".omnisharp");
            if (!Directory.Exists(omnisharpHome))
            {
                Directory.CreateDirectory(omnisharpHome);
            }

            if (String.IsNullOrEmpty(OmnisharpPath))
            {
                var md5 = MD5.Create();

                var pluginAges = new Dictionary<string, string>();
                var sb = new StringBuilder();

                // OrderBy ensures consistent hashing
                foreach (var path in PluginPaths.OrderBy(x => x))
                {
                    sb.AppendLine(path);

                    var projectOrPlugins = Directory.EnumerateFiles(path, "project.json", SearchOption.AllDirectories)
                        .Concat(Directory.EnumerateFiles(path, "omnisharp.plugin", SearchOption.AllDirectories))
                        .OrderBy(x => x)
                        .Where(project => !_nonPlugins.Any(z => project.EndsWith($"{Path.DirectorySeparatorChar}{z}{Path.DirectorySeparatorChar}")));

                    foreach (var projectOrPlugin in projectOrPlugins)
                    {
                        var writeTime = File.GetLastWriteTime(projectOrPlugin);
                        sb.Append(projectOrPlugin);
                        sb.AppendLine(writeTime.Ticks.ToString());
                    }
                }

                foreach (var path in PluginNames.Select(x => string.Join(":", x.Key, x.Value)))
                {
                    sb.Append(path);
                }

                var hash = string.Join("", Convert.ToBase64String(md5.ComputeHash(System.Text.Encoding.UTF8.GetBytes(sb.ToString()))).Except(Path.GetInvalidFileNameChars().Concat(new[] { '=', '+' })));
                OmnisharpPath = Path.Combine(omnisharpHome, hash);
            }

            if (Directory.Exists(OmnisharpPath))
            {
                Console.Write(Path.Combine(OmnisharpPath, "bootstrap", "Bootstrapper"));
                return 0;
            }

            Directory.CreateDirectory(OmnisharpPath);

            var globalJobject = new JObject();
            globalJobject["projects"] = new JArray(new string[] { "bootstrap" }.Concat(PluginPaths).ToArray());

            var sdkJobject = new JObject();
            sdkJobject["version"] = new JValue("1.0.0-beta4");

            globalJobject["sdk"] = sdkJobject;

            File.WriteAllText(Path.Combine(OmnisharpPath, "global.json"), globalJobject.ToString());

            Directory.CreateDirectory(Path.Combine(OmnisharpPath, "bootstrap"));
            Directory.CreateDirectory(Path.Combine(OmnisharpPath, "bootstrap", "Bootstrapper"));

            var pluginDirectories = PluginPaths
                .SelectMany(pluginPath => Directory.EnumerateDirectories(pluginPath)
                    .Where(directory => File.Exists(Path.Combine(directory, "project.json")) || File.Exists(Path.Combine(directory, "1.0.0", "root", "project.json"))))
                    .Where(directory => !_nonPlugins.Any(z => directory.EndsWith($"{Path.DirectorySeparatorChar}{z}")))
                    .ToArray();

            var allDeps = new Dictionary<string, string>();
            allDeps.Add("OmniSharp.Host", "1.0.0-*");
            foreach (var pluginPair in PluginNames)
            {
                allDeps.Add(pluginPair.Key, pluginPair.Value);
            }

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
                    var frameworks = json["frameworks"].Select(x => x.Path.Replace("frameworks.", "")).OrderBy(x => x).ToArray();

                    if (defaultFrameworks.SequenceEqual(frameworks))
                    {
                        if (!allDeps.ContainsKey(name))
                        {
                            allDeps.Add(name, json["version"]?.ToString() ?? "1.0.0-*");
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
                                    if (!deps.ContainsKey(name))
                                    {
                                        deps.Add(name, json["version"]?.ToString() ?? "1.0.0-*");
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
                { "Bootstrapper", "Bootstrapper" }
                });

            var frameworksJobject = new JObject();
            foreach (var deps in frameworkDeps)
            {
                frameworksJobject[deps.Key] = JObject.FromObject(deps.Value);
            }

            projectJobject["frameworks"] = frameworksJobject;
            projectJobject["entryPoint"] = new JValue("OmniSharp.Host");

            var bootstrapProjectPath = Path.Combine(OmnisharpPath, "bootstrap", "Bootstrapper", "project.json");
            var programPath = Path.Combine(OmnisharpPath, "bootstrap", "Bootstrapper", "Startup.cs");
            var bootstrapConfigPath = Path.Combine(OmnisharpPath, "bootstrap", "Bootstrapper", "config.json");
            File.WriteAllText(bootstrapProjectPath, projectJobject.ToString());
            File.WriteAllText(programPath, @"public class Program { public static void Main(string[] args) { OmniSharp.Program.Main(args); } }");
            File.Copy(Path.Combine(Path.GetDirectoryName(OmnisharpProjectPath), "config.json"), bootstrapConfigPath);

            // Scaffold out an app that uses OmniSharp, has a global.json that references all the Plugins that we want to load.
            // Put that in a temporary directory
            // return the full Path to the folder that will Run omnisharp

            Console.Write(Path.Combine(OmnisharpPath, "bootstrap", "Bootstrapper"));
            return 0;
        }
    }
}
