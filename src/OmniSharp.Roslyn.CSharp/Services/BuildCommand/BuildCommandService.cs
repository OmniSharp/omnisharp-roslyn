using System.Composition;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Text;
using OmniSharp.ConfigurationManager;
using OmniSharp.Mef;
using OmniSharp.Models;
using OmniSharp.Models.TestCommand;
using OmniSharp.Services;
using OmniSharp.Models.BuildCommand;
using OmniSharp.Roslyn.Services;

namespace OmniSharp.Roslyn.CSharp.Services.BuildCommand
{
    [OmniSharpHandler(OmniSharpEndpoints.BuildCommand, LanguageNames.CSharp)]
    public class BuildCommandService : IRequestHandler<BuildCommandRequest, QuickFixResponse>//BuildCommandResponse>
    {
        private OmniSharpWorkspace _workspace;
        public OmniSharpConfiguration _config;

        public string Executable
        {
            get
            {
                return Path.Combine(
                        _config.MSBuildPath.Path ?? System.Runtime.InteropServices.RuntimeEnvironment.GetRuntimeDirectory(),
                        "MSBuild"
                    );
            }
        }   

        public string Arguments
        {
            get {
                if (!string.IsNullOrEmpty(ProjectFile))
                {
                    return ("/m ") + "/nologo /v:q /property:GenerateFullPaths=true \"" + ProjectFile + "\"";
                }
                else
                {
                    return ("/m ") + "/nologo /v:q /property:GenerateFullPaths=true";
                }
                //return "build " + ("/m ") + "/nologo /v:q /property:GenerateFullPaths=true \"" + ProjectFile + "\"";
                //return "build";
            }
        }

        public string ProjectFile { get; set; }

        [ImportingConstructor]
        public BuildCommandService(OmniSharpWorkspace workspace, IBuildProvider config)
        {
            _workspace = workspace;
            _config = config.config;
        }

        #region IRequestHandler

        //public async Task<BuildCommandResponse> Handle(BuildCommandRequest request)
        public async Task<QuickFixResponse> Handle(BuildCommandRequest request)
        {
            var document2 = _workspace.CurrentSolution.Projects.SelectMany(p => p.Documents)
                .GroupBy(x => x.FilePath).Select(f => f.FirstOrDefault());
            var document = _workspace.GetDocument(document2.Where(doc => Path.GetFileName(doc.Name).ToLower() == Path.GetFileName(request.FileName).ToLower()).FirstOrDefault().FilePath);
            ProjectFile = document.Project.FilePath;
            var semanticModel = await document.GetSemanticModelAsync();
            var quickFix = new List<QuickFix>();
            quickFix.Add(new QuickFix()
            {
                //Text = this.Executable.ApplyPathReplacementsForClient() + " " + this.Arguments + " /target:" + request.Type.ToString()
                Text = this.Executable.ApplyPathReplacementsForClient() + " " + this.Arguments + " " 
            });
            var response = new QuickFixResponse(quickFix);
            return response;
            //return new BuiudCommandResponse
            //{
            //    //Command = this.Executable.ApplyPathReplacementsForClient() + " " + this.Arguments + " /target:" + request.Type.ToString(),
            //    QuickFixes = quickFix
            //};
        }

        #endregion IRequestHandler

    }
}
