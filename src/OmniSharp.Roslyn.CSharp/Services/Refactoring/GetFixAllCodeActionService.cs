﻿using System.Collections.Generic;
using System.Collections.Immutable;
using System.Composition;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.CodeAnalysis;
using Microsoft.Extensions.Logging;
using OmniSharp.Abstractions.Models.V1.FixAll;
using OmniSharp.Mef;
using OmniSharp.Options;
using OmniSharp.Roslyn.CSharp.Helpers;
using OmniSharp.Roslyn.CSharp.Services.Refactoring.V2;
using OmniSharp.Roslyn.CSharp.Workers.Diagnostics;
using OmniSharp.Services;
using Newtonsoft.Json;

namespace OmniSharp.Roslyn.CSharp.Services.Refactoring
{
    [OmniSharpHandler(OmniSharpEndpoints.GetFixAll, LanguageNames.CSharp)]
    public class GetFixAllCodeActionService : BaseCodeActionService<GetFixAllRequest, GetFixAllResponse>
    {
        [ImportingConstructor]
        public GetFixAllCodeActionService(
            OmniSharpWorkspace workspace,
            [ImportMany] IEnumerable<ICodeActionProvider> providers,
            ILoggerFactory loggerFactory,
            ICsDiagnosticWorker diagnostics,
            CachingCodeFixProviderForProjects codeFixesForProject,
            OmniSharpOptions options
        ) : base(workspace, providers, loggerFactory.CreateLogger<GetFixAllCodeActionService>(), diagnostics, codeFixesForProject, options)
        {
        }

        public override async Task<GetFixAllResponse> Handle(GetFixAllRequest request)
        {
            var document = Workspace.GetDocument(request.FileName);
            if (document is null)
            {
                Logger.LogWarning("Could not find document for file {0}", request.FileName);
                return new GetFixAllResponse(ImmutableArray<FixAllItem>.Empty);
            }

            var allDiagnostics = await GetDiagnosticsAsync(request.Scope, document);
            var rawFixes = allDiagnostics
                .SelectMany(d => d.Diagnostics)
                .Select(d => new FixAllItem("", JsonConvert.SerializeObject(d)))
                .ToArray();
            return new GetFixAllResponse(rawFixes);
        }
    }
}
