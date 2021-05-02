using System;
using System.Collections.Generic;
using Microsoft.Extensions.Configuration;
using OmniSharp.Services;
using Xunit;

namespace OmniSharp.Tests
{
    public class ConfigurationBuilderFacts
    {
        [Fact]
        public void CustomConfigCanBeAdded()
        {
            var env = new OmniSharpEnvironment();
            var builder = new ConfigurationBuilder(env);
            var result = builder.Build(c => c.AddInMemoryCollection(new Dictionary<string, string> { { "key", "value" } }));

            Assert.False(result.HasError());
            Assert.Equal("value", result.Configuration["key"]);
        }

        [Fact]
        public void EnvironmentVariableCanBeRead()
        {
            var tempValue = Guid.NewGuid().ToString("d");
            try
            {
                Environment.SetEnvironmentVariable("OMNISHARP_testValue", tempValue);
                var env = new OmniSharpEnvironment();
                var builder = new ConfigurationBuilder(env);
                var result = builder.Build(c => c.AddInMemoryCollection());

                Assert.False(result.HasError());
                Assert.Equal(tempValue, result.Configuration["testValue"]);
            }
            finally
            {
                Environment.SetEnvironmentVariable("OMNISHARP_testValue", null);
            }
        }

        [Fact]
        public void FileArgsCanBeRead()
        {
            var env = new OmniSharpEnvironment(additionalArguments: new string[] { "key:nestedKey=value" });
            var builder = new ConfigurationBuilder(env);
            var result = builder.Build();

            Assert.False(result.HasError());
            Assert.Equal("value", result.Configuration["key:nestedKey"]);
        }

        [Fact]
        public void DoesNotCrashOnException()
        {
            var env = new OmniSharpEnvironment();
            var builder = new ConfigurationBuilder(env);
            var result = builder.Build(c => throw new Exception("bad thing happened"));

            Assert.True(result.HasError());
            Assert.NotNull(result.Configuration);
            Assert.Empty(result.Configuration.AsEnumerable());
        }
    }
}
