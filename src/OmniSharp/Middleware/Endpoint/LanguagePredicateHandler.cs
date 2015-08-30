using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using OmniSharp.Mef;

namespace OmniSharp.Middleware.Endpoint
{
    class LanguagePredicateHandler
    {
        private readonly IEnumerable<Lazy<Func<string, Task<bool>>, OmniSharpLanguage>> _exports;
        public LanguagePredicateHandler(IEnumerable<Lazy<Func<string, Task<bool>>, OmniSharpLanguage>> exports)
        {
            _exports = exports;
        }

        public async Task<string> GetLanguageForFilePath(string filePath)
        {
            foreach (var export in _exports)
            {
                if (await export.Value(filePath))
                {
                    return export.Metadata.Language;
                }
            }

            return null;
        }
    }
}
