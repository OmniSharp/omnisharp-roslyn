using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Formatting;
using OmniSharp.Models;
using OmniSharp.Models.v1.OverrideImplement;
using OmniSharp.Roslyn.CSharp.Services.OverrideImplement;
using TestUtility;
using Xunit.Abstractions;

namespace OmniSharp.Roslyn.CSharp.Tests
{
    public class AbstractOverrideImplementTestFixture : AbstractSingleRequestHandlerTestFixture<OverrideImplementService>
    {
        protected AbstractOverrideImplementTestFixture(ITestOutputHelper output, SharedOmniSharpHostFixture sharedOmniSharpHostFixture)
            : base(output, sharedOmniSharpHostFixture)
        {
        }

        protected override string EndpointName => OmniSharpEndpoints.OverrideImplement;

        protected async Task<OverrideImplementResponce> OverrideImplementAsync(string filename, string source, string target)
        {
            var testFile = new TestFile(filename, source);
            SharedOmniSharpTestHost.AddFilesToWorkspace(testFile);
            var options = SharedOmniSharpTestHost.Workspace.Options;
            SharedOmniSharpTestHost.Workspace.Options = options.WithChangedOption(FormattingOptions.NewLine, LanguageNames.CSharp, "\r\n");
            var point = testFile.Content.GetPointFromPosition();

            var request = new OverrideImplementRequest
            {
                Line = point.Line,
                Column = point.Offset,
                FileName = testFile.FileName,
                Buffer = testFile.Content.Code,
                OverrideTarget = target
            };

            var requestHandler = GetRequestHandler(SharedOmniSharpTestHost);

            return await requestHandler.Handle(request);
        }

        protected LinePositionSpanTextChange CreateChange(int startLine, int startColumn, int endLine, int endColumn, string newText)
        {
            return new LinePositionSpanTextChange()
            {
                StartLine = startLine,
                StartColumn = startColumn,
                EndLine = endLine,
                EndColumn = endColumn,
                NewText = newText,
            };
        }

        protected LinePositionSpanTextChange CreateChange(string source, string newText)
        {
            var lines = Regex.Split(source, "\n");
            int line = -1;
            int startColumn = -1;
            int endColumn = -1;
            for (int i = 0; i < lines.Length; i++)
            {
                if ((endColumn = lines[i].IndexOf("$$")) >= 0)
                {
                    line = i;
                    startColumn = Regex.Match(lines[i], "^(\\s*)[\\S+]").Groups[1].Length;
                    break;
                }
            }
            return CreateChange(line, startColumn, line, endColumn, newText);
        }

            protected OverrideImplementResponce CreateExpect(params LinePositionSpanTextChange[] changes)
        {
            return new OverrideImplementResponce()
            {
                Changes = changes,
            };
        }

        protected OverrideImplementResponce CreateExpect(int startLine, int startColumn, int endLine, int endColumn, string newText)
        {
            return new OverrideImplementResponce()
            {
                Changes = new List<LinePositionSpanTextChange>()
                {
                    CreateChange(startLine, startColumn, endLine, endColumn, newText)
                }
            };
        }

        protected OverrideImplementResponce CreateExpect(string source, string newText)
        {
            return new OverrideImplementResponce()
            {
                Changes = new List<LinePositionSpanTextChange>()
                {
                    CreateChange(source, newText)
                }
            };
        }
    }

    public static class OverrideImplementResponceExtension
    {
        public static void Insert(this OverrideImplementResponce self, LinePositionSpanTextChange change, int position)
        {
            var changes = self.Changes.ToList();
            changes.Insert(position, change);
            self.Changes = changes;
        }

        public static void Add(this OverrideImplementResponce self, LinePositionSpanTextChange change)
        {
            var changes = self.Changes.ToList();
            changes.Add(change);
            self.Changes = changes;
        }
    }
}
