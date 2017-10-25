using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using Cake.Scripting.Abstractions.Models;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Text;
using OmniSharp.Cake.Services;

namespace OmniSharp.Cake
{
    internal class CakeTextLoader : TextLoader
    {
        private readonly string _filePath;
        private readonly ICakeScriptService _scriptService;

        public CakeTextLoader(string filePath, ICakeScriptService generationService)
        {
            if (filePath == null)
            {
                throw new ArgumentNullException(nameof(filePath));
            }

            if (!Path.IsPathRooted(filePath))
            {
                throw new ArgumentException("Expected an absolute file path", nameof(filePath));
            }

            _filePath = filePath;
            _scriptService = generationService ?? throw new ArgumentNullException(nameof(generationService));
        }

        public override Task<TextAndVersion> LoadTextAndVersionAsync(Workspace workspace, DocumentId documentId, CancellationToken cancellationToken)
        {
            var prevLastWriteTime = File.GetLastWriteTimeUtc(_filePath);

            var script = _scriptService.Generate(new FileChange
            {
                FileName = _filePath,
                FromDisk = true
            });
            var version = VersionStamp.Create(prevLastWriteTime);
            var text = SourceText.From(script.Source);
            var textAndVersion = TextAndVersion.Create(text, version, _filePath);

            var newLastWriteTime = File.GetLastWriteTimeUtc(_filePath);
            if (!newLastWriteTime.Equals(prevLastWriteTime))
            {
                throw new IOException($"File was externally modified: {_filePath}");
            }

            return Task.FromResult(textAndVersion);
        }
    }
}
