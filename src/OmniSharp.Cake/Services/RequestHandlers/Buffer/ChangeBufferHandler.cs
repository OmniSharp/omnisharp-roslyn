using System.Composition;
using System.Threading.Tasks;
using Cake.Scripting.Abstractions.Models;
using OmniSharp.Mef;
using OmniSharp.Models.ChangeBuffer;
using OmniSharp.Models.UpdateBuffer;

namespace OmniSharp.Cake.Services.RequestHandlers.Buffer
{
    [OmniSharpHandler(OmniSharpEndpoints.ChangeBuffer, Constants.LanguageNames.Cake)]
    public class ChangeBufferHandler : IRequestHandler<ChangeBufferRequest, object>
    {
        private readonly OmniSharpWorkspace _workspace;
        private readonly ICakeScriptService _scriptService;

        [ImportingConstructor]
        public ChangeBufferHandler(
            OmniSharpWorkspace workspace,
            ICakeScriptService scriptService)
        {
            _workspace = workspace;
            _scriptService = scriptService;
        }

        public async Task<object> Handle(ChangeBufferRequest request)
        {
            if (request.FileName == null)
            {
                return true;
            }

            var script = _scriptService.Generate(new FileChange
            {
                FileName = request.FileName,
                LineChanges = { new LineChange
                {
                    StartLine = request.StartLine,
                    StartColumn = request.StartColumn,
                    EndLine = request.EndLine,
                    EndColumn = request.EndColumn,
                    NewText = request.NewText
                }}
            });

            // Redirect to UpdateBuffer
            await _workspace.BufferManager.UpdateBufferAsync(new UpdateBufferRequest
            {
                Buffer = script.Source,
                FileName = request.FileName
            });
            return true;
        }
    }
}
