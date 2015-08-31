using System;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.CodeAnalysis;
using OmniSharp.Mef;

namespace OmniSharp.Roslyn.CSharp.Services
{
    public class CSharpLanguage
    {
        private static readonly string[] ValidCSharpExtensions = { "cs", "csx", "cake" };
        [OmniSharpLanguage(LanguageNames.CSharp)]
        public Func<string, bool> IsApplicableTo { get; } = filePath => ValidCSharpExtensions.Any(extension => filePath.EndsWith(extension));
    }
}
