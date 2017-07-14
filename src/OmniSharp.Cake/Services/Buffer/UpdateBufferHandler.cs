using System.Composition;
using System.Threading.Tasks;
using OmniSharp.Mef;
using Cake.Scripting.Abstractions;
using Cake.Scripting.Abstractions.Models;
using OmniSharp.Models.UpdateBuffer;

namespace OmniSharp.Cake.Services.Buffer
{
    [OmniSharpHandler(OmniSharpEndpoints.UpdateBuffer, Constants.LanguageNames.Cake)]
    public class UpdateBufferHandler : IRequestHandler<UpdateBufferRequest, object>
    {
        private readonly OmniSharpWorkspace _workspace;
        private readonly IScriptGenerationService _generationService;

        [ImportingConstructor]
        public UpdateBufferHandler(
            OmniSharpWorkspace workspace,
            IScriptGenerationService generationService)
        {
            _workspace = workspace;
            _generationService = generationService;
        }

        public async Task<object> Handle(UpdateBufferRequest request)
        {
            if (request.FileName == null)
            {
                return true;
            }

            var fileChange = new FileChange
            {
                FileName = request.FileName
            };

            if(request.Changes != null)
            {
                foreach(var change in request.Changes)
                {
                    fileChange.LineChanges.Add(new LineChange
                    {
                        StartLine = change.StartLine,
                        StartColumn = change.StartColumn,
                        EndLine = change.EndLine,
                        EndColumn = change.EndColumn,
                        NewText = change.NewText
                    });
                }
            }
            else
            {
                fileChange.Buffer = request.Buffer;
                fileChange.FromDisk = request.FromDisk;
            }
            var script = _generationService.Generate(fileChange);

            // Avoid having buffer manager reading from disk
            request.FromDisk = false;
            request.Buffer = script.Source;
            request.Changes = null;

            await _workspace.BufferManager.UpdateBufferAsync(request);
            return true;
        }
    }
}
