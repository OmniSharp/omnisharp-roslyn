using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Threading.Tasks;
using Microsoft.Framework.Runtime;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace OmniSharp.Bootstrap
{
    public class Program
    {
        private readonly IApplicationEnvironment _appEnv;
        private readonly string[] _nonPlugins = { "OmniSharp.Plugins", "OmniSharp.Abstractions", "OmniSharp.Stdio", "OmniSharp" };

        public Program(IApplicationEnvironment appEnv)
        {
            _appEnv = appEnv;
        }

        public int Main(string[] args)
        {
            var enumerator = args.GetEnumerator();
            var pluginPaths = new List<string>();

            var bootstrapPath = Path.GetDirectoryName(_appEnv.ApplicationBasePath);
            var omnisharpProjectPath = Path.Combine(bootstrapPath, "OmniSharp", "project.json");
            if (!File.Exists(omnisharpProjectPath))
            {
                omnisharpProjectPath = Path.Combine(bootstrapPath, "OmniSharp", "1.0.0", "root", "project.json");
            }

            while (enumerator.MoveNext())
            {
                var arg = (string)enumerator.Current;

                if (arg == "--plugins")
                {
                    enumerator.MoveNext();
                    pluginPaths.Add((string)enumerator.Current);
                }
            }

            if (!pluginPaths.Any()) {
                Console.WriteLine(Path.GetDirectoryName(omnisharpProjectPath));
                return 0;
            }

            var defaultFrameworks = JObject.Parse(File.ReadAllText(omnisharpProjectPath))["frameworks"]
                .Select(x => x.Path.Replace("frameworks.", ""))
                .OrderBy(x => x).ToArray();

            pluginPaths.Add(bootstrapPath);

            // Find a repeatable user based location
            var home = new string[] { Environment.GetEnvironmentVariable("HOME"), Environment.GetEnvironmentVariable("USERPROFILE") }.Where(s => !string.IsNullOrEmpty(s)).First();
            var omnisharpHome = Path.Combine(home, ".omnisharp");
            if (!Directory.Exists(omnisharpHome))
            {
                Directory.CreateDirectory(omnisharpHome);
            }

            var md5 = MD5.Create();

            var pluginAges = new Dictionary<string, string>();
            var sb = new StringBuilder();

            // OrderBy ensures consistent hashing
            foreach (var path in pluginPaths.OrderBy(x => x))
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

            var hash = string.Join("", Convert.ToBase64String(md5.ComputeHash(System.Text.Encoding.UTF8.GetBytes(sb.ToString()))).Except(Path.GetInvalidFileNameChars()));
            var omnisharpPath = Path.Combine(omnisharpHome, hash);
            if (Directory.Exists(omnisharpPath))
            {
                Console.WriteLine(Path.Combine(omnisharpPath, "bootstrap", "Bootstrapper"));
                return 0;
            }

            Directory.CreateDirectory(omnisharpPath);
            var globalJobject = new JObject();
            globalJobject["projects"] = new JArray(new string[] { "bootstrap" }.Concat(pluginPaths).ToArray());

            var sdkJobject = new JObject();
            sdkJobject["version"] = new JValue("1.0.0-beta4");

            globalJobject["sdk"] = sdkJobject;

            File.WriteAllText(Path.Combine(omnisharpPath, "global.json"), globalJobject.ToString());

            Directory.CreateDirectory(Path.Combine(omnisharpPath, "bootstrap"));
            Directory.CreateDirectory(Path.Combine(omnisharpPath, "bootstrap", "Bootstrapper"));

            var pluginDirectories = pluginPaths
                .SelectMany(pluginPath => Directory.EnumerateDirectories(pluginPath)
                    .Where(directory => File.Exists(Path.Combine(directory, "project.json")) || File.Exists(Path.Combine(directory, "1.0.0", "root", "project.json"))))
                    .Where(directory => !_nonPlugins.Any(z => directory.EndsWith($"{Path.DirectorySeparatorChar}{z}")))
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
                    var frameworks = json["frameworks"].Select(x => x.Path.Replace("frameworks.", "")).OrderBy(x => x).ToArray();

                    if (defaultFrameworks.SequenceEqual(frameworks))
                    {
                        if (!allDeps.ContainsKey(name))
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
                                    if (!deps.ContainsKey(name)) {
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

            var bootstrapProjectPath = Path.Combine(omnisharpPath, "bootstrap", "Bootstrapper", "project.json");
            File.WriteAllText(bootstrapProjectPath, projectJobject.ToString());
            File.Copy(Path.Combine(Path.GetDirectoryName(omnisharpProjectPath), "config.json"), Path.Combine(omnisharpPath, "bootstrap", "Bootstrapper", "config.json"));

            // Scaffold out an app that uses OmniSharp, has a global.json that references all the Plugins that we want to load.
            // Put that in a temporary directory
            // return the full Path to the folder that will Run omnisharp

            Console.WriteLine(Path.Combine(omnisharpPath, "bootstrap", "Bootstrapper"));

            PackageRestore(bootstrapProjectPath, "1.0.0-beta4");

            return 0;
        }

        private int PackageRestore(string project, string version)
        {
            var psi = new ProcessStartInfo()
            {
                FileName = FirstPath(GetRuntimePath(version), "dnu", "dnu.cmd"),
                WorkingDirectory = Path.GetDirectoryName(project),
                CreateNoWindow = true,
                UseShellExecute = false,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                Arguments = "restore"
            };

            var restoreProcess = Process.Start(psi);
            if (restoreProcess.HasExited)
            {
                return restoreProcess.ExitCode;
            }

            restoreProcess.BeginOutputReadLine();
            restoreProcess.BeginErrorReadLine();
            restoreProcess.WaitForExit();

            return 0;
        }

        private string GetRuntimePath(string version)
        {
            var seachedLocations = new List<string>();

            foreach (var location in GetRuntimeLocations())
            {
                //  Need to expand variables, because DNX_HOME variable might include %USERPROFILE%.
                var paths = GetRuntimePathsFromVersionOrAlias(version, Environment.ExpandEnvironmentVariables(location));

                foreach (var path in paths)
                {
                    if (string.IsNullOrEmpty(path))
                    {
                        continue;
                    }

                    if (Directory.Exists(path))
                    {
                        return path;
                    }

                    seachedLocations.Add(path);
                }
            }

            throw new Exception(string.Format("The specified runtime path '{0}' does not exist. Searched locations {1}.\nVisit https://github.com/aspnet/Home for an installation guide.", version, string.Join("\n", seachedLocations)));
        }

        private IEnumerable<string> GetRuntimeLocations()
        {
            yield return Environment.GetEnvironmentVariable("DNX_HOME") ?? string.Empty;

            //  %HOME% and %USERPROFILE% might point to different places.
            foreach (var home in new string[] { Environment.GetEnvironmentVariable("HOME"), Environment.GetEnvironmentVariable("USERPROFILE") }.Where(s => !string.IsNullOrEmpty(s)))
            {
                // Newer path
                yield return Path.Combine(home, ".dnx");
            }
        }

        private IEnumerable<string> GetRuntimePathsFromVersionOrAlias(string versionOrAlias, string runtimePath)
        {
            // Newer format
            yield return GetRuntimePathFromVersionOrAlias(versionOrAlias, runtimePath, ".dnx", "dnx-mono.{0}", "dnx-clr-win-x86.{0}", "runtimes");
        }

        private string GetRuntimePathFromVersionOrAlias(string versionOrAlias,
                                                        string runtimeHome,
                                                        string sdkFolder,
                                                        string monoFormat,
                                                        string windowsFormat,
                                                        string runtimeFolder)
        {
            if (string.IsNullOrEmpty(runtimeHome))
            {
                return null;
            }

            var aliasDirectory = Path.Combine(runtimeHome, "alias");

            var aliasFiles = new[] { "{0}.alias", "{0}.txt" };

            // Check alias first
            foreach (var shortAliasFile in aliasFiles)
            {
                var aliasFile = Path.Combine(aliasDirectory, string.Format(shortAliasFile, versionOrAlias));

                if (File.Exists(aliasFile))
                {
                    var fullName = File.ReadAllText(aliasFile).Trim();

                    return Path.Combine(runtimeHome, runtimeFolder, fullName);
                }
            }

            // There was no alias, look for the input as a version
            var version = versionOrAlias;

            if (PlatformHelper.IsMono)
            {
                return Path.Combine(runtimeHome, runtimeFolder, string.Format(monoFormat, versionOrAlias));
            }
            else
            {
                return Path.Combine(runtimeHome, runtimeFolder, string.Format(windowsFormat, versionOrAlias));
            }
        }

        internal static string FirstPath(string runtimePath, params string[] candidates)
        {
            if (runtimePath == null)
            {
                return null;
            }
            return candidates
                .Select(candidate => Path.Combine(runtimePath, "bin", candidate))
                .FirstOrDefault(File.Exists);
        }
    }
}
