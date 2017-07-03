using System;
using System.Collections.Generic;
using System.Composition;
using System.Threading.Tasks;
using Microsoft.Extensions.Configuration;
using OmniSharp.Models.WorkspaceInformation;
using OmniSharp.Services;

namespace OmniSharp.Cake
{
    [Export(typeof(IProjectSystem)), Shared]
    public class CakeProjectSystem : IProjectSystem
    {
        public string Key => "Cake";
        public string Language => Constants.LanguageNames.Cake;
        public IEnumerable<string> Extensions => new[] { ".cake" };

        public void Initalize(IConfiguration configuration)
        {
            throw new NotImplementedException();
        }

        public Task<object> GetWorkspaceModelAsync(WorkspaceInformationRequest request)
        {
            throw new NotImplementedException();
        }

        public Task<object> GetProjectModelAsync(string filePath)
        {
            throw new NotImplementedException();
        }
    }
}
