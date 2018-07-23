using OmniSharp.Services;
using System;
using System.ComponentModel.Composition;

namespace OmniSharp.Mef
{
    [MetadataAttribute]
    [AttributeUsage(AttributeTargets.Class, AllowMultiple = false)]
    public class ExportIProjectSystemAttribute: ExportAttribute
    {
        public string Name;

        public ExportIProjectSystemAttribute(string name) : base(name, typeof(IProjectSystem))
        {
            Name = name;
        }
    }
}
