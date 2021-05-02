using System.Composition;
using System.Threading.Tasks;
using Cake.Scripting.Abstractions.Models;
using OmniSharp.Mef;
using OmniSharp.Models.UpdateBuffer;

namespace OmniSharp.Cake.Services.RequestHandlers.Buffer
{
    [OmniSharpHandler(OmniSharpEndpoints.UpdateBuffer, Constants.LanguageNames.Cake)]
    public class UpdateBufferHandler : IRequestHandler<UpdateBufferRequest, object>
    {
        private readonly OmniSharpWorkspace _workspace;
        private readonly ICakeScriptService _scriptService;

        [ImportingConstructor]
        public UpdateBufferHandler(
            OmniSharpWorkspace workspace,
            ICakeScriptService scriptService)
        {
            _workspace = workspace;
            _scriptService = scriptService;
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

                fileChange.FromDisk = false;
            }
            else
            {
                fileChange.Buffer = request.Buffer;
                fileChange.FromDisk = request.FromDisk;
            }
            var script = _scriptService.Generate(fileChange);

            // Avoid having buffer manager reading from disk
            request.FromDisk = false;
            request.Buffer = script.Source;
            request.Changes = null;

            await _workspace.BufferManager.UpdateBufferAsync(request);
            return true;
        }
    }
}
