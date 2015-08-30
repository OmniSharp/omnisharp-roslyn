using System;
using System.Composition;
using System.Threading.Tasks;

namespace OmniSharp.Mef
{
    public class OmniSharpLanguage
    {
        public string Language { get; set; }
    }

    [MetadataAttribute]
    public class OmniSharpEndpointAttribute : ExportAttribute
    {
        public string Language { get; }

        public OmniSharpEndpointAttribute(Type type, string language) : base(type)
        {
            Language = language;
        }
    }

    [MetadataAttribute]
    public class OmniSharpLanguageAttribute : ExportAttribute
    {
        public string Language { get; }

        public OmniSharpLanguageAttribute(string language) : base(typeof(Func<string, Task<bool>>))
        {
            Language = language;
        }
    }
}
