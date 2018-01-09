// Adapted from ExtensionOrderer in Roslyn
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CodeFixes;
using Microsoft.CodeAnalysis.CodeRefactorings;

namespace OmniSharp.Roslyn.CSharp.Services.Refactoring.V2
{
    internal class ProviderNode<TProvider>
    {
        public string ProviderName { get; set; }
        public List<string> Before { get; set; }
        public List<string> After { get; set; }
        public TProvider Provider { get; set; }
        public HashSet<ProviderNode<TProvider>> NodesBeforeMeSet { get; set; }

        public static ProviderNode<TProvider> From(TProvider provider)
        {
            string providerName = "";
            if (provider is CodeFixProvider)
            {
                var exportAttribute = provider.GetType().GetCustomAttribute(typeof(ExportCodeFixProviderAttribute));
                if (exportAttribute is ExportCodeFixProviderAttribute fixAttribute && fixAttribute.Name != null)
                {
                    providerName = fixAttribute.Name;
                }
            }
            else
            {
                var exportAttribute = provider.GetType().GetCustomAttribute(typeof(ExportCodeRefactoringProviderAttribute));
                if (exportAttribute is ExportCodeRefactoringProviderAttribute refactoringAttribute && refactoringAttribute.Name != null)
                {
                    providerName = refactoringAttribute.Name;
                }
            }

            var orderAttributes = provider.GetType().GetCustomAttributes(typeof(ExtensionOrderAttribute), true).Select(attr => (ExtensionOrderAttribute)attr).ToList();
            return new ProviderNode<TProvider>(provider, providerName, orderAttributes);
        }

        private ProviderNode(TProvider provider, string providerName, List<ExtensionOrderAttribute> orderAttributes)
        {
            Provider = provider;
            ProviderName = providerName;
            Before = new List<string>();
            After = new List<string>();
            NodesBeforeMeSet = new HashSet<ProviderNode<TProvider>>();
            orderAttributes.ForEach(attr => AddAttribute(attr));
        }

        private void AddAttribute(ExtensionOrderAttribute attribute)
        {
            if (attribute.Before != null)
                Before.Add(attribute.Before);
            if (attribute.After != null)
                After.Add(attribute.After);
        }

        internal bool CheckForCycles()
        {
            return CheckForCycles(new HashSet<ProviderNode<TProvider>>());
        }

        private bool CheckForCycles(HashSet<ProviderNode<TProvider>> seenNodes)
        {
            if (!seenNodes.Add(this))
            {
                //Cycle detected
                return true;
            }

            foreach (var before in this.NodesBeforeMeSet)
            {
                if (before.CheckForCycles(seenNodes))
                    return true;
            }

            seenNodes.Remove(this);
            return false;
        }
    }
}
