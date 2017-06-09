using System;
using System.Collections.Generic;
using System.Linq;
using OmniSharp.Services;

namespace OmniSharp.Middleware.Endpoint
{
    class LanguagePredicateHandler : IPredicateHandler
    {
        private readonly IEnumerable<IProjectSystem> _projectSystems;

        public LanguagePredicateHandler(IEnumerable<IProjectSystem> projectSystems)
        {
            _projectSystems = projectSystems;
        }

        public string GetLanguageForFilePath(string filePath)
        {
            foreach (var projectSystem in _projectSystems)
            {
                if (projectSystem.Extensions.Any(extension => filePath.EndsWith(extension, StringComparison.OrdinalIgnoreCase)))
                {
                    return projectSystem.Language;
                }
            }

            return null;
        }
    }
}
