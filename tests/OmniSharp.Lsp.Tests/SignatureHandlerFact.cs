using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Extensions.Configuration;
using OmniSharp.Extensions.LanguageServer.Protocol;
using OmniSharp.Extensions.LanguageServer.Protocol.Document;
using OmniSharp.Extensions.LanguageServer.Protocol.Models;
using OmniSharp.Extensions.LanguageServer.Protocol.Workspace;
using OmniSharp.Models.V2;
using TestUtility;
using Xunit;
using Xunit.Abstractions;
using Xunit.Sdk;
using Range = OmniSharp.Extensions.LanguageServer.Protocol.Models.Range;

namespace OmniSharp.Lsp.Tests
{
    public class SignatureHandlerFact : AbstractLanguageServerTestBase
    {
        public SignatureHandlerFact(ITestOutputHelper output)
            : base(output) { }

        [Fact]
        public async Task Get_signature_help()
        {
            const string source =
@"class Program
{
    public static void Main()
    {
        var flag = Compare($$);
    }
    ///<summary>Checks if object is tagged with the tag.</summary>
    /// <param name=""gameObject"">The game object.</param>
    /// <param name=""tagName"">Name of the tag.</param>
    /// <returns>Returns <c>true</c> if object is tagged with tag.</returns>
    public static bool Compare(GameObject gameObject, string tagName)
    {
        return true;
    }
}";
            var actual = await GetSignatureAsync(source);
            Assert.Single(actual.Signatures);
            Assert.Equal(0, actual.ActiveParameter);
            Assert.Equal(0, actual.ActiveSignature);
            Assert.Equal("Checks if object is tagged with the tag.", actual.Signatures.ElementAt(0).Documentation);
        }

        private Task<SignatureHelp> GetSignatureAsync(string code)
        {
            return GetSignatureAsync(new[] { new TestFile("dummy.cs", code) });
        }

        private async Task<SignatureHelp> GetSignatureAsync(TestFile[] testFiles)
        {
            OmniSharpTestHost.AddFilesToWorkspace(testFiles
                .Select(f =>
                    new TestFile(
                        ((f.FileName.StartsWith("/") || f.FileName.StartsWith("\\")) ? f.FileName : ("/" + f.FileName))
                        .Replace(Path.AltDirectorySeparatorChar, Path.DirectorySeparatorChar), f.Content))
                .ToArray()
            );
            var file = testFiles.Single(tf => tf.Content.HasPosition);
            var point = file.Content.GetPointFromPosition();

            Client.TextDocument.DidChangeTextDocument(new DidChangeTextDocumentParams()
            {
                ContentChanges = new Container<TextDocumentContentChangeEvent>(new TextDocumentContentChangeEvent()
                {
                    Text = file.Content.Code
                }),
                TextDocument = new OptionalVersionedTextDocumentIdentifier()
                {
                    Uri = DocumentUri.From(file.FileName),
                    Version = 1
                }
            });
            return await Client.TextDocument.RequestSignatureHelp(new SignatureHelpParams()
            {
                TextDocument = file.FileName,
                Position = new Position(point.Line, point.Offset),
                Context = new SignatureHelpContext { }
            }, CancellationToken);
        }
    }
}
