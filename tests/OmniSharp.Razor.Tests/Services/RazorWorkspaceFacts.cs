using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using OmniSharp.Razor.Services;
using OmniSharp.Tests;
using Xunit;

namespace OmniSharp.Razor.Tests.Services
{
    public class RazorWorkspaceFacts
    {
        [Fact]
        public async Task ShouldCreateAPageWhenOpened()
        {
            var result = await RazorTestHelpers.CreateTestWorkspace("RazorProjectSample01");
            result.OmnisharpWorkspace.OpenDocument(
                result.OmnisharpWorkspace.GetDocumentId(Path.Combine(result.Path, "Test.cshtml"))
            );

            await Task.Delay(TimeSpan.FromMilliseconds(100));

            Assert.Equal(1, result.RazorWorkspace.OpenDocumentIds.Count());
        }

        [Fact]
        public async Task ShouldReturnaNewContextOnceThePageIsClosed()
        {
            var result = await RazorTestHelpers.CreateTestWorkspace("RazorProjectSample01");
            result.OmnisharpWorkspace.OpenDocument(
                result.OmnisharpWorkspace.GetDocumentId(Path.Combine(result.Path, "Test.cshtml"))
            );

            Assert.Equal(1, result.RazorWorkspace.OpenDocumentIds.Count());

            await Task.Delay(TimeSpan.FromMilliseconds(100));

            result.OmnisharpWorkspace.CloseDocument(
                result.OmnisharpWorkspace.GetDocumentId(Path.Combine(result.Path, "Test.cshtml"))
            );

            await Task.Delay(TimeSpan.FromMilliseconds(100));

            Assert.Equal(0, result.RazorWorkspace.OpenDocumentIds.Count());
        }
    }
}
