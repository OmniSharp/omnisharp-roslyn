// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;
using System.Composition;
using System.IO;
using OmniSharp.Cake.Configuration.Parser;

namespace OmniSharp.Cake.Configuration
{
    [Export(typeof(ICakeConfiguration)), Shared]
    internal sealed class CakeConfiguration : ICakeConfiguration
    {
        private readonly Dictionary<string, string> _lookup;

        [ImportingConstructor]
        public CakeConfiguration(IOmniSharpEnvironment environment)
        {
            _lookup = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

            // Parse the configuration file.
            var configurationPath = Path.Combine(environment.TargetDirectory, "cake.config");
            if (File.Exists(configurationPath))
            {
                var parser = new ConfigurationParser();
                var configuration = parser.Read(configurationPath);
                foreach (var key in configuration.Keys)
                {
                    _lookup[KeyNormalizer.Normalize(key)] = configuration[key];
                }
            }
        }

        public string GetValue(string key)
        {
            key = KeyNormalizer.Normalize(key);
            return _lookup.ContainsKey(key)
                ? _lookup[key] : null;
        }
    }
}