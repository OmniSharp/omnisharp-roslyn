using OmniSharp.Services;
using System;
using System.Composition;

namespace OmniSharp.Mef
{
    [MetadataAttribute]
    [AttributeUsage(AttributeTargets.Class, AllowMultiple = false)]
    public class ExportIProjectSystemAttribute: ExportAttribute
    {
        public string Name { get; }
        public string Before { get; }
        public string After { get; }

        public ExportIProjectSystemAttribute(string name, string before="", string after="") : base(typeof(IProjectSystem))
        {
            Name = name;
            Before = before;
            After = after;
        }
    }

    public class ProjectSystemMetadata 
    {
         string Name { get; set; }
    }
}
