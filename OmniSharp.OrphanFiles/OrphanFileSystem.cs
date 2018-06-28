using System;
using System.Collections.Generic;
using System.Composition;
using System.Threading.Tasks;
using Microsoft.Extensions.Configuration;
using OmniSharp.Models.WorkspaceInformation;
using OmniSharp.Services;

namespace OmniSharp.OrphanFiles
{
    [Export(typeof(IProjectSystem)), Shared]
    public class OrphanFileSystem : IProjectSystem
    {
        string IProjectSystem.Key => throw new NotImplementedException();

        string IProjectSystem.Language => throw new NotImplementedException();

        IEnumerable<string> IProjectSystem.Extensions => throw new NotImplementedException();

        bool IProjectSystem.EnabledByDefault => throw new NotImplementedException();

        private readonly OmniSharpWorkspace _workspace;

        [ImportingConstructor]
        public OrphanFileSystem(OmniSharpWorkspace workspace)
        {
            _workspace = workspace;
        }

        Task<object> IProjectSystem.GetProjectModelAsync(string filePath)
        {
            throw new NotImplementedException();
        }

        Task<object> IProjectSystem.GetWorkspaceModelAsync(WorkspaceInformationRequest request)
        {
            throw new NotImplementedException();
        }

        void IProjectSystem.Initalize(IConfiguration configuration)
        {
            throw new NotImplementedException();
        }
    }
}
