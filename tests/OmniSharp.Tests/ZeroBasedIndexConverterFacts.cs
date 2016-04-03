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
            Configuration.ZeroBasedIndicies = true;

            var request = new Request()
            {
                Line = 1,
                Column = 1,
            };

            var output = JsonConvert.SerializeObject(request);

            // Pretend the client is really emacs / vim
            Configuration.ZeroBasedIndicies = false;

            var input = JsonConvert.DeserializeObject<Request>(output);

            Assert.Equal(input.Line, 0);
            Assert.Equal(input.Column, 0);
        }

        [Fact]
        public void ShouldInteractWithZeroBasedIndexes()
        {
            Configuration.ZeroBasedIndicies = true;

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
            Configuration.ZeroBasedIndicies = false;

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
