using Newtonsoft.Json;
using OmniSharp.Models;
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
    }
}
