using BenchmarkDotNet.Attributes;
using BenchmarkDotNet.Diagnostics.Windows.Configs;
using OmniSharp.Models.v1.Completion;
using OmniSharp.Roslyn.CSharp.Services.Completion;
using System.Collections.Generic;
using System.Text;
using System.Threading.Tasks;
using TestUtility;

namespace OmniSharp.Benchmarks
{
    [EtwProfiler]
    public class OverrideCompletionBenchmarks : HostBase
    {
        [Params(10, 100, 250, 500)]
        public int NumOverrides { get; set; }

        public CompletionRequest Request { get; set; } = null!;

        [GlobalSetup]
        public async Task SetupAsync()
        {
            Setup(new KeyValuePair<string, string?>("RoslynExtensionsOptions:EnableImportCompletion", "true"));

            var builder = new StringBuilder();

            builder.AppendLine("namespace N1");
            builder.AppendLine("{");
            builder.AppendLine("    using System.Collections.Generic;");
            builder.AppendLine("    class Base");
            builder.AppendLine("    {");
            for (int i = 0; i < NumOverrides; i++)
            {
                builder.AppendLine($"        public virtual Dictionary<string, string> M{i}(List<string> s) {{ return null; }}");
            }
            builder.AppendLine("    }");
            builder.AppendLine("}");
            builder.AppendLine("namespace N2 : N1.Base");
            builder.AppendLine("{");
            builder.AppendLine("    class Derived");
            builder.AppendLine("    {");
            builder.AppendLine("        override $$");
            builder.AppendLine("    }");
            builder.AppendLine("}");

            const string FileName = "OverrideTest.cs";
            var file = new TestFile(FileName, builder.ToString());
            OmniSharpTestHost.AddFilesToWorkspace(file);

            var point = file.Content.GetPointFromPosition();

            Request = new()
            {
                CompletionTrigger = OmniSharp.Models.v1.Completion.CompletionTriggerKind.Invoked,
                Line = point.Line,
                Column = point.Offset,
                FileName = FileName
            };

            // Trigger completion once to ensure that all the runs have a warmed-up server
            await OverrideCompletionAsync();
        }

        [Benchmark]
        public async Task<CompletionResponse> OverrideCompletionAsync()
        {
            var handler = OmniSharpTestHost.GetRequestHandler<CompletionService>(OmniSharpEndpoints.Completion);
            return await handler.Handle(Request);
        }
    }
}
