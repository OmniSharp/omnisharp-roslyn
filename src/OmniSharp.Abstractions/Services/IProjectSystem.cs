using System.Collections.Generic;
using System.Threading.Tasks;
using Microsoft.Extensions.Configuration;
using OmniSharp.Models.WorkspaceInformation;

namespace OmniSharp.Services
{
    public interface IProjectSystem
    {
        string Key { get; }
        string Language { get; }
        IEnumerable<string> Extensions { get; }
        bool EnabledByDefault { get; }

        /// <summary>
        /// Flag indicating that the project system has been sucessfully initialized.
        /// </summary>
        bool Initialized { get; }

        /// <summary>
        /// Initialize the project system.
        /// </summary>
        /// <param name="configuration">The configuration to use.</param>
        void Initalize(IConfiguration configuration);

        /// <summary>
        /// Get a model of the entire workspace loaded in this project system.
        /// </summary>
        Task<object> GetWorkspaceModelAsync(WorkspaceInformationRequest request);

        /// <summary>
        /// Get a model of a specific project in this project system.
        /// </summary>
        /// <param name="filePath">The file path to the project to retrieve. Alternatively,
        /// a file path to a document within a proejct may be specified.</param>
        Task<object> GetProjectModelAsync(string filePath);
    }
}
