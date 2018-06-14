using Newtonsoft.Json;
using OmniSharp.Models;
using OmniSharp.Models.ChangeBuffer;
using OmniSharp.Models.CodeAction;
using OmniSharp.Models.Highlight;
using OmniSharp.Models.Navigate;
using OmniSharp.Models.V2;
using Xunit;

namespace OmniSharp.Tests
{
    public class ZeroBasedIndexConverterFacts
    {
        [Fact]
        public void ShouldInteractWithEmacsLikeRequests()
        {
            Configuration.ZeroBasedIndices = true;

            var request = new Request()
            {
                Line = 1,
                Column = 1,
            };

            var output = JsonConvert.SerializeObject(request);

            // Pretend the client is really emacs / vim
            Configuration.ZeroBasedIndices = false;

            var input = JsonConvert.DeserializeObject<Request>(output);

            Assert.Equal(0, input.Line);
            Assert.Equal(0, input.Column);
        }

        [Fact]
        public void ShouldInteractWithZeroBasedIndexes()
        {
            Configuration.ZeroBasedIndices = true;

            var request = new Request()
            {
                Line = 0,
                Column = 0,
            };

            var output = JsonConvert.SerializeObject(request);
            var input = JsonConvert.DeserializeObject<Request>(output);

            Assert.Equal(request.Line, input.Line);
            Assert.Equal(request.Column, input.Column);
        }

        [Fact]
        public void ShouldInteractWithOneBasedIndexes()
        {
            Configuration.ZeroBasedIndices = false;

            var request = new Request()
            {
                Line = 1,
                Column = 1,
            };

            var output = JsonConvert.SerializeObject(request);
            var input = JsonConvert.DeserializeObject<Request>(output);

            Assert.Equal(request.Line, input.Line);
            Assert.Equal(request.Column, input.Column);
        }

        [Fact]
        public void Point_OneBased()
        {
            Configuration.ZeroBasedIndices = false;

            const string input = @"
{
  ""line"": 1,
  ""column"": 1
}
";

            var point = JsonConvert.DeserializeObject<Point>(input);

            Assert.Equal(0, point.Line);
            Assert.Equal(0, point.Column);
        }

        [Fact]
        public void Point_ZeroBased()
        {
            Configuration.ZeroBasedIndices = true;

            const string input = @"
{
  ""line"": 1,
  ""column"": 1
}
";

            var point = JsonConvert.DeserializeObject<Point>(input);

            Assert.Equal(1, point.Line);
            Assert.Equal(1, point.Column);
        }

        [Fact]
        public void Range_OneBased()
        {
            Configuration.ZeroBasedIndices = false;

            const string input = @"
{
  ""start"": {
    ""line"": 1,
    ""column"": 1
  },
  ""end"": {
    ""line"": 19,
    ""column"": 23
  }
}
";

            var range = JsonConvert.DeserializeObject<Range>(input);

            Assert.Equal(0, range.Start.Line);
            Assert.Equal(0, range.Start.Column);
            Assert.Equal(18, range.End.Line);
            Assert.Equal(22, range.End.Column);
        }

        [Fact]
        public void Range_ZeroBased()
        {
            Configuration.ZeroBasedIndices = true;

            const string input = @"
{
  ""start"": {
    ""line"": 1,
    ""column"": 1
  },
  ""end"": {
    ""line"": 19,
    ""column"": 23
  }
}
";

            var range = JsonConvert.DeserializeObject<Range>(input);

            Assert.Equal(1, range.Start.Line);
            Assert.Equal(1, range.Start.Column);
            Assert.Equal(19, range.End.Line);
            Assert.Equal(23, range.End.Column);
        }

        [Fact]
        public void HighlightSpan_OneBased()
        {
            Configuration.ZeroBasedIndices = false;

            const string input = @"
{
  ""startLine"": 1,
  ""startColumn"": 1,
  ""endLine"": 19,
  ""endColumn"": 23
}
";

            var highlightSpan = JsonConvert.DeserializeObject<HighlightSpan>(input);

            Assert.Equal(0, highlightSpan.StartLine);
            Assert.Equal(0, highlightSpan.StartColumn);
            Assert.Equal(18, highlightSpan.EndLine);
            Assert.Equal(22, highlightSpan.EndColumn);
        }

        [Fact]
        public void HighlightSpan_ZeroBased()
        {
            Configuration.ZeroBasedIndices = true;

            const string input = @"
{
  ""startLine"": 1,
  ""startColumn"": 1,
  ""endLine"": 19,
  ""endColumn"": 23
}
";

            var highlightSpan = JsonConvert.DeserializeObject<HighlightSpan>(input);

            Assert.Equal(1, highlightSpan.StartLine);
            Assert.Equal(1, highlightSpan.StartColumn);
            Assert.Equal(19, highlightSpan.EndLine);
            Assert.Equal(23, highlightSpan.EndColumn);
        }

        [Fact]
        public void ChangeBufferRequest_OneBased()
        {
            Configuration.ZeroBasedIndices = false;

            const string input = @"
{
  ""startLine"": 1,
  ""startColumn"": 1,
  ""endLine"": 19,
  ""endColumn"": 23
}
";

            var changeBufferRequest = JsonConvert.DeserializeObject<ChangeBufferRequest>(input);

            Assert.Equal(0, changeBufferRequest.StartLine);
            Assert.Equal(0, changeBufferRequest.StartColumn);
            Assert.Equal(18, changeBufferRequest.EndLine);
            Assert.Equal(22, changeBufferRequest.EndColumn);
        }

        [Fact]
        public void ChangeBufferRequest_ZeroBased()
        {
            Configuration.ZeroBasedIndices = true;

            const string input = @"
{
  ""startLine"": 1,
  ""startColumn"": 1,
  ""endLine"": 19,
  ""endColumn"": 23
}
";

            var changeBufferRequest = JsonConvert.DeserializeObject<ChangeBufferRequest>(input);

            Assert.Equal(1, changeBufferRequest.StartLine);
            Assert.Equal(1, changeBufferRequest.StartColumn);
            Assert.Equal(19, changeBufferRequest.EndLine);
            Assert.Equal(23, changeBufferRequest.EndColumn);
        }

        [Fact]
        public void GetCodeActionRequest_OneBased()
        {
            Configuration.ZeroBasedIndices = false;

            const string input = @"
{
  ""selectionStartLine"": 1,
  ""selectionStartColumn"": 1,
  ""selectionEndLine"": 19,
  ""selectionEndColumn"": 23
}
";

            var codeActionRequest = JsonConvert.DeserializeObject<GetCodeActionRequest>(input);

            Assert.Equal(0, codeActionRequest.SelectionStartLine);
            Assert.Equal(0, codeActionRequest.SelectionStartColumn);
            Assert.Equal(18, codeActionRequest.SelectionEndLine);
            Assert.Equal(22, codeActionRequest.SelectionEndColumn);
        }

        [Fact]
        public void GetCodeActionRequest_ZeroBased()
        {
            Configuration.ZeroBasedIndices = true;

            const string input = @"
{
  ""selectionStartLine"": 1,
  ""selectionStartColumn"": 1,
  ""selectionEndLine"": 19,
  ""selectionEndColumn"": 23
}
";

            var codeActionRequest = JsonConvert.DeserializeObject<GetCodeActionRequest>(input);

            Assert.Equal(1, codeActionRequest.SelectionStartLine);
            Assert.Equal(1, codeActionRequest.SelectionStartColumn);
            Assert.Equal(19, codeActionRequest.SelectionEndLine);
            Assert.Equal(23, codeActionRequest.SelectionEndColumn);
        }

        [Fact]
        public void NavigateResponse_OneBased()
        {
            Configuration.ZeroBasedIndices = false;

            var input = new NavigateResponse()
            {
                Line = 0,
                Column = 0
            };

            var json = JsonConvert.SerializeObject(input, Formatting.Indented);

            const string output = @"
{
  ""Line"": 1,
  ""Column"": 1
}
";

            Assert.Equal(output.Trim(), json);
        }

        [Fact]
        public void NavigateResponse_ZeroBased()
        {
            Configuration.ZeroBasedIndices = true;

            var input = new NavigateResponse()
            {
                Line = 0,
                Column = 0
            };

            var json = JsonConvert.SerializeObject(input, Formatting.Indented);

            var output = @"
{
  ""Line"": 0,
  ""Column"": 0
}
".Trim();

            Assert.Equal(output, json);
        }

        [Fact]
        public void LinePositionSpanTextChange_OneBased()
        {
            Configuration.ZeroBasedIndices = false;

            const string input = @"
{
  ""startLine"": 1,
  ""startColumn"": 1,
  ""endLine"": 19,
  ""endColumn"": 23
}
";

            var linePositionSpanTextChange = JsonConvert.DeserializeObject<LinePositionSpanTextChange>(input);

            Assert.Equal(0, linePositionSpanTextChange.StartLine);
            Assert.Equal(0, linePositionSpanTextChange.StartColumn);
            Assert.Equal(18, linePositionSpanTextChange.EndLine);
            Assert.Equal(22, linePositionSpanTextChange.EndColumn);
        }

        [Fact]
        public void LinePositionSpanTextChange_ZeroBased()
        {
            Configuration.ZeroBasedIndices = true;

            const string input = @"
{
  ""startLine"": 1,
  ""startColumn"": 1,
  ""endLine"": 19,
  ""endColumn"": 23
}
";

            var linePositionSpanTextChange = JsonConvert.DeserializeObject<LinePositionSpanTextChange>(input);

            Assert.Equal(1, linePositionSpanTextChange.StartLine);
            Assert.Equal(1, linePositionSpanTextChange.StartColumn);
            Assert.Equal(19, linePositionSpanTextChange.EndLine);
            Assert.Equal(23, linePositionSpanTextChange.EndColumn);
        }

        [Fact]
        public void QuickFix_OneBased()
        {
            Configuration.ZeroBasedIndices = false;

            var input = new QuickFix()
            {
                Line = 0,
                Column = 0,
                EndLine = 19,
                EndColumn = 23
            };

            var json = JsonConvert.SerializeObject(input, Formatting.Indented);

            const string output = @"
{
  ""FileName"": null,
  ""Line"": 1,
  ""Column"": 1,
  ""EndLine"": 20,
  ""EndColumn"": 24,
  ""Text"": null,
  ""Projects"": []
}
";

            Assert.Equal(output.Trim(), json);
        }

        [Fact]
        public void QuickFix_ZeroBased()
        {
            Configuration.ZeroBasedIndices = true;

            var input = new QuickFix()
            {
                Line = 0,
                Column = 0,
                EndLine = 19,
                EndColumn = 23
            };

            var json = JsonConvert.SerializeObject(input, Formatting.Indented);

            const string output = @"
{
  ""FileName"": null,
  ""Line"": 0,
  ""Column"": 0,
  ""EndLine"": 19,
  ""EndColumn"": 23,
  ""Text"": null,
  ""Projects"": []
}
";

            Assert.Equal(output.Trim(), json);
        }
    }
}
