using BenchmarkDotNet.Attributes;
using BenchmarkDotNet.Diagnostics.Windows.Configs;
using OmniSharp.Models.v1.Completion;
using OmniSharp.Roslyn.CSharp.Services.Completion;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using TestUtility;

namespace OmniSharp.Benchmarks
{
    [EtwProfiler]
    public class ImportCompletionBenchmarks : HostBase
    {
        public CompletionRequest Request { get; set; } = null!;

        [GlobalSetup]
        public async Task SetupAsync()
        {
            Setup(new KeyValuePair<string, string?>("RoslynExtensionsOptions:EnableImportCompletion", "true"));

            var builder = new StringBuilder();

            builder.AppendLine("class Base");
            builder.AppendLine("{");
            builder.AppendLine("    void M()");
            builder.AppendLine("    {");
            builder.AppendLine("        $$");
            builder.AppendLine("    }");
            builder.AppendLine("}");

            const string FileName = "ImportCompletionTest.cs";
            var file = new TestFile(FileName, builder.ToString());
            OmniSharpTestHost.AddFilesToWorkspace(file);

            var point = file.Content.GetPointFromPosition();

            Request = new()
            {
                CompletionTrigger = CompletionTriggerKind.Invoked,
                Line = point.Line,
                Column = point.Offset,
                FileName = FileName
            };

            // Trigger completion once to ensure that all the runs have a warmed-up server, with full completions loaded
            CompletionResponse completions;
            do
            {
                completions = await ImportCompletionListAsync();
            } while (!completions.Items.Any(i => i.Label == "Console"));

        }

        [Benchmark]
        public async Task<CompletionResponse> ImportCompletionListAsync()
        {
            var handler = OmniSharpTestHost.GetRequestHandler<CompletionService>(OmniSharpEndpoints.Completion);
            return await handler.Handle(Request);
        }
    }
}
