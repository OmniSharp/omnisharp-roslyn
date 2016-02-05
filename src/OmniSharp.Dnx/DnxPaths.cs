using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Microsoft.CodeAnalysis;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using OmniSharp.Models;
using OmniSharp.Options;
using OmniSharp.Services;

namespace OmniSharp.Dnx
{
    public class DnxPaths
    {
        private readonly IOmnisharpEnvironment _env;
        private readonly DnxOptions _options;
        private readonly ILogger _logger;
        public DnxRuntimePathResult RuntimePath { get; private set; }
        public string Dnx { get; private set; }
        public string Dnu { get; private set; }

        public DnxPaths(IOmnisharpEnvironment env,
                        DnxOptions options,
                        ILoggerFactory loggerFactory)
        {
            _env = env;
            _options = options;
            _logger = loggerFactory.CreateLogger<DnxPaths>();

            RuntimePath = GetRuntimePath();
            Dnx = FirstPath(RuntimePath.Value, "dnx", "dnx.exe");
            Dnu = FirstPath(RuntimePath.Value, "dnu", "dnu.cmd");
        }

        private DnxRuntimePathResult GetRuntimePath()
        {
            var root = ResolveRootDirectory(_env.Path);
            var globalJson = Path.Combine(root, "global.json");
            var aliasToken = GetRuntimeSpec(globalJson, "alias");
            var alias = aliasToken?.Value<string>() ?? _options?.Alias ?? "default";
            var versionToken = GetRuntimeSpec(globalJson, "version");
            var version = versionToken?.Value<string>();
            if (version != null)
            {
                var nameToken = GetRuntimeSpec(globalJson, "runtime");
                var name = nameToken?.Value<string>() ?? (PlatformHelper.IsMono ? "mono" : "clr");
                if (name.Contains("clr"))
                {
                    name = string.Format("{0}-{1}-{2}", name,
                            PlatformHelper.OSString, 
                            GetRuntimeSpec(globalJson, "architecture")?.Value<string>() ?? "x86");
                }
                version = string.Format("{0}.{1}", name, version);
            }
            var seachedLocations = new List<string>();

            foreach (var location in GetRuntimeLocations())
            {
                //  Need to expand variables, because DNX_HOME variable might include %USERPROFILE%.
                var paths = GetRuntimePathsFromVersionOrAlias(version, alias, Environment.ExpandEnvironmentVariables(location));

                foreach (var path in paths)
                {
                    if (string.IsNullOrEmpty(path))
                    {
                        continue;
                    }

                    if (Directory.Exists(path))
                    {
                        _logger.LogInformation(string.Format("Using runtime '{0}'.", path));
                        return new DnxRuntimePathResult()
                        {
                            Value = path
                        };
                    }

                    seachedLocations.Add(path);
                }
            }

            var message = new ErrorMessage();
            if (versionToken != null)
            {
                message.Text = string.Format("The specified runtime path '{0}' does not exist. Searched locations {1}.\nVisit https://github.com/aspnet/Home for an installation guide.", version, string.Join("\n", seachedLocations));
                message.FileName = globalJson;
                message.Line = ((IJsonLineInfo)versionToken).LineNumber;
                message.Column = ((IJsonLineInfo)versionToken).LinePosition;
            }
            else
            {
                message.Text = string.Format("The specified runtime alias '{0}' does not exist.\nVisit https://github.com/aspnet/Home for an installation guide.", alias);
                message.FileName = globalJson;
                message.Line = ((IJsonLineInfo)aliasToken).LineNumber;
                message.Column = ((IJsonLineInfo)aliasToken).LinePosition;
            }
            _logger.LogError(message.Text);
            return new DnxRuntimePathResult()
            {
                Error = message
            };
        }

        private JToken GetRuntimeSpec(string globalJson, string spec)
        {
            if (File.Exists(globalJson))
            {
                _logger.LogInformation("Looking for sdk {0} in '{1}'.", spec, globalJson);

                using (var stream = File.OpenRead(globalJson))
                {
                    using (var streamReader = new StreamReader(stream))
                    {
                        using (var textReader = new JsonTextReader(streamReader))
                        {
                            var obj = JObject.Load(textReader);
                            return obj["sdk"]?[spec];
                        }
                    }
                }
            }

            return null;
        }

        private static string ResolveRootDirectory(string projectPath)
        {
            var di = new DirectoryInfo(projectPath);
            while (di.Parent != null)
            {
                if (di.EnumerateFiles("global.json").Any())
                {
                    return di.FullName;
                }

                di = di.Parent;
            }
            // If we don't find any files then make the project folder the root
            return projectPath;
        }

        private IEnumerable<string> GetRuntimeLocations()
        {
            yield return Environment.GetEnvironmentVariable("DNX_HOME") ?? string.Empty;
            yield return Environment.GetEnvironmentVariable("KRE_HOME") ?? string.Empty;

            //  %HOME% and %USERPROFILE% might point to different places.
            foreach (var home in new string[] { Environment.GetEnvironmentVariable("HOME"), Environment.GetEnvironmentVariable("USERPROFILE") }.Where(s => !string.IsNullOrEmpty(s)))
            {
                // Newer path
                yield return Path.Combine(home, ".dnx");
            }

            if (!string.IsNullOrEmpty(Environment.GetEnvironmentVariable("ProgramData")))
            {
                yield return Path.Combine(Environment.GetEnvironmentVariable("ProgramData"), "Microsoft DNX");
            }

            if (!string.IsNullOrEmpty(Environment.GetEnvironmentVariable("AllUsersProfile")))
            {
                yield return Path.Combine(Environment.GetEnvironmentVariable("AllUsersProfile"), "Microsoft DNX");
            }

            if (!string.IsNullOrEmpty(Environment.GetEnvironmentVariable("ProgramFiles")))
            {
                yield return Path.Combine(Environment.GetEnvironmentVariable("ProgramFiles"), "Microsoft DNX");
            }

            yield return Path.Combine(@"/usr/local/lib/dnx");
        }

        private IEnumerable<string> GetRuntimePathsFromVersionOrAlias(string version, string alias, string runtimePath)
        {
            // Newer format
            yield return GetRuntimePathFromVersionOrAlias(version, alias, runtimePath, "dnx-{0}", "runtimes");
        }

        private string GetRuntimePathFromVersionOrAlias(string version,
                                                        string alias,
                                                        string runtimeHome,
                                                        string runtimeFormat,
                                                        string runtimeFolder)
        {
            if (string.IsNullOrEmpty(runtimeHome))
            {
                return null;
            }

            // Return version if given
            if (version != null)
            {
                return Path.Combine(runtimeHome, runtimeFolder, string.Format(runtimeFormat, version));
            }

            var aliasDirectory = Path.Combine(runtimeHome, "alias");

            var aliasFiles = new[] { "{0}.alias", "{0}.txt" };

            // Check alias if no version given
            foreach (var shortAliasFile in aliasFiles)
            {
                var aliasFile = Path.Combine(aliasDirectory, string.Format(shortAliasFile, alias));

                if (File.Exists(aliasFile))
                {
                    var fullName = File.ReadAllText(aliasFile).Trim();

                    return Path.Combine(runtimeHome, runtimeFolder, fullName);
                }
            }
            
            return null;
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
