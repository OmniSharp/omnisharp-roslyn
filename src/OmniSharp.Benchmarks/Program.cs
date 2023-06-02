using BenchmarkDotNet.Running;
using Microsoft.Extensions.Configuration;
using OmniSharp.Benchmarks;
using OmniSharp.Utilities;
using System.Collections.Generic;
using TestUtility;

BenchmarkRunner.Run(typeof(OverrideCompletionBenchmarks).Assembly);

namespace OmniSharp.Benchmarks
{
    public abstract class HostBase : DisposableObject
    {
        protected OmniSharpTestHost OmniSharpTestHost { get; set; } = null!;

        protected override void DisposeCore(bool disposing)
        {
            OmniSharpTestHost.Dispose();
        }

        public void Setup(params KeyValuePair<string, string?>[]? configuration)
        {
            var builder = new Microsoft.Extensions.Configuration.ConfigurationBuilder().AddInMemoryCollection(configuration);
            OmniSharpTestHost = OmniSharpTestHost.Create(configurationData: builder.Build());
        }
    }
}
