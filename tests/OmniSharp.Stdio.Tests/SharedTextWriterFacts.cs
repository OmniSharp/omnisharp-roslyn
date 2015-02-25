using System;
using System.IO;
using System.Text;
using System.Threading.Tasks;
using OmniSharp.Stdio.Services;
using Xunit;

namespace OmniSharp.Stdio.Tests
{
    public class SharedTextWriterFacts
    {
        [Fact]
        public void SyncOrder()
        {
            var buffer = new StringBuilder();
            using (var writer = new StringWriter(buffer))
            {
                var sharedWriter = new SharedTextWriter(writer);
                sharedWriter.Use(w => w.Write("far"));
                sharedWriter.Use(w => w.Write("boo"));
            }
            Assert.Equal("farboo", buffer.ToString());
        }

        [Fact]
        public void SyncExceptionsAreHandled()
        {
            var buffer = new StringBuilder();
            using (var writer = new StringWriter(buffer))
            {
                var sharedWriter = new SharedTextWriter(writer);
                Action<TextWriter> action = w => { throw new Exception(); };
                Assert.Throws(typeof(Exception), () => sharedWriter.Use(action));
                sharedWriter.Use(w => w.Write("stillalive"));
            }
            Assert.Equal("stillalive", buffer.ToString());
        }

        [Fact]
        public void AsyncOrder()
        {
            var buffer = new StringBuilder();
            using (var writer = new StringWriter(buffer))
            {
                var sharedWriter = new SharedTextWriter(writer);
                sharedWriter.Use(w =>
                {
                    w.Write("far");
                    return Task.FromResult(true);
                });

                sharedWriter.Use(w =>
                {
                    w.Write("boo");
                    return Task.FromResult(true);
                });
            }
            Assert.Equal("farboo", buffer.ToString());
        }

        [Fact]
        public void AsyncExceptionsAreHandled1()
        {
            var buffer = new StringBuilder();
            using (var writer = new StringWriter(buffer))
            {
                var sharedWriter = new SharedTextWriter(writer);
                Assert.ThrowsAsync(typeof(Exception), () => sharedWriter.Use(w =>
                {
                    var source = new TaskCompletionSource<object>();
                    source.SetException(new Exception());
                    return source.Task;
                }));
                sharedWriter.Use(w =>
                {
                    w.Write("stillalive");
                    return Task.FromResult(true);
                });
            }
            Assert.Equal("stillalive", buffer.ToString());
        }

        [Fact]
        public void AsyncExceptionsAreHandled2()
        {
            var buffer = new StringBuilder();
            using (var writer = new StringWriter(buffer))
            {
                var sharedWriter = new SharedTextWriter(writer);
                Assert.ThrowsAsync(typeof(Exception), () => sharedWriter.Use(w =>
                {
                    throw new Exception();
                }));

                sharedWriter.Use(w =>
                {
                    w.Write("stillalive");
                    return Task.FromResult(true);
                });
            }
            Assert.Equal("stillalive", buffer.ToString());
        }
    }
}
