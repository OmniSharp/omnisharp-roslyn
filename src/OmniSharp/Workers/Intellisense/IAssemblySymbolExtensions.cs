using System;
using System.Collections.Generic;
using Microsoft.CodeAnalysis;

namespace OmniSharp.Intellisense
{
    public static class IAssemblySymbolExtensions
    {
        private const string AttributeSuffix = "Attribute";

        public static bool ContainsNamespaceName(
            this List<IAssemblySymbol> assemblies,
            string namespaceName)
        {
            // PERF: Expansion of "assemblies.Any(a => a.NamespaceNames.Contains(namespaceName))"
            // to avoid allocating a lambda.
            foreach (var a in assemblies)
            {
                if (a.NamespaceNames.Contains(namespaceName))
                {
                    return true;
                }
            }

            return false;
        }

        public static bool ContainsTypeName(this List<IAssemblySymbol> assemblies, string typeName, bool tryWithAttributeSuffix = false)
        {
            if (!tryWithAttributeSuffix)
            {
                // PERF: Expansion of "assemblies.Any(a => a.TypeNames.Contains(typeName))"
                // to avoid allocating a lambda.
                foreach (var a in assemblies)
                {
                    if (a.TypeNames.Contains(typeName))
                    {
                        return true;
                    }
                }
            }
            else
            {
                var attributeName = typeName + AttributeSuffix;
                foreach (var a in assemblies)
                {
                    var typeNames = a.TypeNames;
                    if (typeNames.Contains(typeName) || typeNames.Contains(attributeName))
                    {
                        return true;
                    }
                }
            }

            return false;
        }

        public static bool IsSameAssemblyOrHasFriendAccessTo(this IAssemblySymbol assembly, IAssemblySymbol toAssembly)
        {
            return
                Equals(assembly, toAssembly) ||
                (assembly.IsInteractive && toAssembly.IsInteractive) ||
                toAssembly.GivesAccessTo(assembly);
        }
    }
}
