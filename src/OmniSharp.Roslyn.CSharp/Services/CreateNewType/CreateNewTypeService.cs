using System;
using System.Composition;
using System.IO;
using System.Threading.Tasks;
using Microsoft.CodeAnalysis;
using Microsoft.Extensions.Logging;
using OmniSharp.Mef;
using OmniSharp.Models.CreateNewTypeRequest;

namespace OmniSharp.Roslyn.CSharp.Services.CreateNewType
{
    [OmniSharpHandler(OmniSharpEndpoints.CreateNewType, LanguageNames.CSharp)]
    public class CreateNewTypeService : IRequestHandler<CreateNewTypeRequest, CreateNewTypeResponse>
    {
        private const string FILE_CONTENT_TEMPLATE =
@"namespace {0};

public {1} {2}
{{

}}
";

        private readonly OmniSharpWorkspace _workspace;
        private readonly ILogger<CreateNewTypeService> _logger;

        [ImportingConstructor]
        public CreateNewTypeService(
                OmniSharpWorkspace workspace,
                ILoggerFactory loggerFactory)
        {
            _workspace = workspace;
            _logger = loggerFactory.CreateLogger<CreateNewTypeService>();
        }

        public async Task<CreateNewTypeResponse> Handle(CreateNewTypeRequest request)
        {
            if (string.IsNullOrWhiteSpace(request.FileParentPath))
            {
                throw new ArgumentException("FileParentPath can not be empty");
            }
            if (string.IsNullOrWhiteSpace(request.SymbolName))
            {
                throw new ArgumentException("SymbolName can not be empty");
            }

            string newSymbolFileName = request.Type switch
            {
                TypeEnum.Interface => $"I{request.SymbolName}.cs",
                _ => $"{request.SymbolName}.cs",
            };
            string newSymbolPath = Path.Combine(request.FileParentPath, newSymbolFileName);

            if (_workspace.GetDocument(newSymbolPath) is not null)
            {
                throw new ArgumentException($"{newSymbolPath} already exists in project");
            }

            Project closestProject = _workspace.FingProjectByPath(newSymbolPath);
            if (closestProject is null)
            {
                throw new ArgumentException($"{request.FileParentPath} doesn't belong to any project");
            }
            _logger.LogDebug($"Lang: {closestProject.Language}");

            string closestProjectDefaultNamespace = closestProject.DefaultNamespace;
            string symbolRelativeNamespace = new FileInfo(newSymbolPath)
                .DirectoryName
                .Replace(new FileInfo(closestProject.FilePath).Directory.Parent.FullName, "")
                    .Trim('/')
                    .Replace("/", ".");
            string symbolNamespace = string.IsNullOrWhiteSpace(closestProjectDefaultNamespace)
                ? $"{closestProjectDefaultNamespace}."
                : "";

            symbolNamespace = $"{symbolNamespace}{symbolRelativeNamespace}";

            _logger.LogDebug($"Creating {newSymbolPath} with namespace {symbolNamespace}");
            using StreamWriter newSymbolStream = new(newSymbolPath);
            string fileContent = string.Format(FILE_CONTENT_TEMPLATE, symbolNamespace, request.Type.ToString().ToLower(), request.SymbolName);
            await newSymbolStream.WriteAsync(fileContent);
            _workspace.AddDocument(closestProject, newSymbolPath);

            return new CreateNewTypeResponse();
        }
    }
}

