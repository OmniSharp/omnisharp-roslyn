using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CodeFixes;
using Microsoft.CodeAnalysis.CodeRefactorings;

namespace OmniSharp.Roslyn.CSharp.Services.Refactoring.V2
{
    internal class ProviderNode<T>
    {
        public string ProviderName { get; set; }
        public List<string> Before { get; set; }
        public List<string> After { get; set; }
        public T Provider { get; set; }
        public HashSet<ProviderNode<T>> NodesBeforeMeSet { get; set; }

        public static ProviderNode<T> From(T provider)
        {
            var exportAttribute = provider.GetType().GetCustomAttribute(typeof(ExportCodeFixProviderAttribute));
            var providerName = exportAttribute is ExportCodeFixProviderAttribute ? ((ExportCodeFixProviderAttribute)exportAttribute).Name : "";
            var orderAttributes = provider.GetType().GetCustomAttributes(typeof(ExtensionOrderAttribute), true).Select(attr => (ExtensionOrderAttribute)attr).ToList();
            return new ProviderNode<T>(provider, providerName, orderAttributes);
        }

        private ProviderNode(T provider, string providerName, List<ExtensionOrderAttribute> orderAttributes)
        {
            Provider = provider;
            ProviderName = providerName;
            Before = new List<string>();
            After = new List<string>();
            NodesBeforeMeSet = new HashSet<ProviderNode<T>>();
            orderAttributes.ForEach(attr => AddAttribute(attr));
        }

        private void AddAttribute(ExtensionOrderAttribute attribute)
        {
            if(attribute.Before != null)
                Before.Add(attribute.Before);
            if (attribute.After != null)
                After.Add(attribute.After);
        }
    }
}
