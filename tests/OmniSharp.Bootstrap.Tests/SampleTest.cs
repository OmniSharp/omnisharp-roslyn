using System;
using System.IO;
using System.Linq;
using System.Runtime.Versioning;
using Microsoft.Extensions.PlatformAbstractions;
using Xunit;

namespace OmniSharp.Bootstrap.Tests
{
    class FakeApplicationEnvironment : IApplicationEnvironment
    {
        public string ApplicationBasePath
        {
            get
            {
                return Directory.GetCurrentDirectory();
            }
        }

        public string ApplicationName
        {
            get
            {
                throw new NotImplementedException();
            }
        }

        public string ApplicationVersion
        {
            get
            {
                throw new NotImplementedException();
            }
        }

        public string Configuration
        {
            get
            {
                throw new NotImplementedException();
            }
        }

        public FrameworkName RuntimeFramework
        {
            get
            {
                throw new NotImplementedException();
            }
        }

        public string Version
        {
            get
            {
                throw new NotImplementedException();
            }
        }
    }

    public class SampleTest
    {
        [Fact]
        public void AddsPlugins()
        {
            var program = new Program(new FakeApplicationEnvironment());

            program.ParseArguments(new string[] { "--plugins", "/a/b/c/d" });
            Assert.Contains("/a/b/c/d", program.PluginPaths);
        }

        [Fact]
        public void AddsAPluginByName()
        {
            var program = new Program(new FakeApplicationEnvironment());

            program.ParseArguments(new string[] { "--plugin-name", "PluginA", "--plugin-name", "PluginB" });
            Assert.True(program.PluginNames.Any(z => z.Key == "PluginA"));
            Assert.True(program.PluginNames.Any(z => z.Key == "PluginB"));
        }

        [Fact]
        public void AddsAPluginByNameWithVersion()
        {
            var program = new Program(new FakeApplicationEnvironment());

            program.ParseArguments(new string[] { "--plugin-name", "PluginA@1.0.0", "--plugin-name", "PluginB" });
            Assert.True(program.PluginNames.Any(z => z.Key == "PluginA" && z.Value == "1.0.0"));
        }

        [Fact]
        public void SetsSolutionRoot()
        {
            var program = new Program(new FakeApplicationEnvironment());

            program.ParseArguments(new string[] { "-s", "/path/to/project" });
            Assert.Equal(Path.GetFullPath("/path/to/project"), program.SolutionRoot);
        }
    }
}
