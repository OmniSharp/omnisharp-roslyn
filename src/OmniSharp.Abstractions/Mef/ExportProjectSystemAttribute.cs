using System;
using System.Composition;
using OmniSharp.Services;

namespace OmniSharp.Mef
{
    [MetadataAttribute]
    [AttributeUsage(AttributeTargets.Class, AllowMultiple = false)]
    public class ExportProjectSystemAttribute: ExportAttribute
    {
        public string Name { get; }
        
        public ExportProjectSystemAttribute(string name) : base(typeof(IProjectSystem))
        {
            Name = name;
        }
    }
}
