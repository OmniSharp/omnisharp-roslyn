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
    [AttributeUsage(AttributeTargets.Method | AttributeTargets.Class)]
    public class OmniSharpEndpointAttribute : ExportAttribute
    {
        public string Language { get; }

        public OmniSharpEndpointAttribute(Type type, string language) : base(type)
        {
            Language = language;
        }
    }

    [MetadataAttribute]
    [AttributeUsage(AttributeTargets.Method)]
    public class OmniSharpLanguageAttribute : ExportAttribute
    {
        public string Language { get; }

        public OmniSharpLanguageAttribute(string language) : base(typeof(Func<string, Task<bool>>))
        {
            Language = language;
        }
    }
}
