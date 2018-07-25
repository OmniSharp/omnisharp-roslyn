using System;
using System.Composition;
using Microsoft.CodeAnalysis;
using OmniSharp.Services;

namespace OmniSharp.Mef
{
    [MetadataAttribute]
    [AttributeUsage(AttributeTargets.Class, AllowMultiple = false)]
    public class ExportIProjectSystemAttribute: ExportAttribute
    {
        public string Name { get; }
        
        public ExportIProjectSystemAttribute(string name) : base(typeof(IProjectSystem))
        {
            Name = name;
        }
    }
}
