using Microsoft.CodeAnalysis;
using OmniSharp.Extensions;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;

namespace OmniSharp.Roslyn.CSharp.Helpers
{
    public static class OverrideHelpers
    {
        private static readonly string _throwNotImplementedException = $"{ReservedWords.Throw} {ReservedWords.New} System.NotImplementedException();";

        public static ISymbol FindTargetSymbol(ITypeSymbol symbol, string target)
        {
            foreach (var type in symbol.GetBaseTypes())
            {
                foreach (var member in type.GetMembers())
                {
                    if (member.IsOverridable() && target.Equals(member.ToDisplayString()))
                    {
                        return member;
                    }
                }
            }
            return null;
        }

        public static string GetOverrideImplementString(ISymbol symbol, ISet<string> namespaces)
        {
            string code = string.Empty;
            switch (symbol.Kind)
            {
                case SymbolKind.Method:
                    code = GetOverrideMethod((IMethodSymbol)symbol, namespaces);
                    break;
                case SymbolKind.Property:
                    code = GetOverrideProperty((IPropertySymbol)symbol, namespaces);
                    break;
            }
            return code;
        }

        private static string GetOverrideMethod(IMethodSymbol method, ISet<string> namespaces)
        {
            var accessibility = method.GetAccessibilityString();
            var methodName = method.Name;
            var returnType = method.ReturnType.ToMinimalDisplayString();

            var parameters = string.Join(", ", method.Parameters.Select(x => $"{x.Type.ToMinimalDisplayString()} {x.Name}"));
            var parameterNames = string.Join(", ", method.Parameters.Select(x => x.Name));
            var typeParameters = string.Join(", ", method.TypeParameters);
            if (!string.IsNullOrEmpty(typeParameters))
            {
                typeParameters = $"<{typeParameters}>";
            }

            var invocation = _throwNotImplementedException;
            if (!method.IsAbstract)
            {
                var @return = ReservedWords.Void.Equals(returnType) ? "" : $"{ReservedWords.Return} ";
                invocation = $"{@return}{ReservedWords.Base}.{methodName}{typeParameters}({parameterNames});";
            }

            CollectNamespaces(method.ReturnType, namespaces);

            foreach (var param in method.Parameters)
            {
                CollectNamespaces(param.Type, namespaces);
            }
            return $"{accessibility} {ReservedWords.Override} {returnType} {methodName}{typeParameters}({parameters})\n{{\n{invocation}\n}}";
        }

        private static string GetOverrideProperty(IPropertySymbol property, ISet<string> namespaces)
        {
            var text = new StringBuilder();
            var accessibility = property.GetAccessibilityString();
            var propertyName = property.Name;
            var propertyType = property.Type.ToMinimalDisplayString();

            CollectNamespaces(property.Type, namespaces);

            text.Append($"{accessibility} {ReservedWords.Override} {propertyType} {propertyName}\n{{\n");

            var getter = property.GetMethod;
            if (getter.IsOverridable())
            {
                var getterAccessibility = getter.GetAccessibilityString();
                if (accessibility != getterAccessibility)
                {
                    text.Append($"{getterAccessibility} ");
                }
                var invocation = _throwNotImplementedException;
                if (!getter.IsAbstract)
                {
                    invocation = $"{ReservedWords.Return} {ReservedWords.Base}.{propertyName};";
                }
                text.Append($"{ReservedWords.Get}{{{invocation}}}\n");
            }
            var setter = property.SetMethod;
            if (setter.IsOverridable())
            {
                var setterAccessibility = setter.GetAccessibilityString();
                if (accessibility != setterAccessibility)
                {
                    text.Append($"{setterAccessibility} ");
                }
                var invocation = _throwNotImplementedException;
                if (!setter.IsAbstract)
                {
                    invocation = $"{ReservedWords.Base}.{propertyName}={ReservedWords.Value};";
                }
                text.Append($"{ReservedWords.Set}{{{invocation}}}\n");
            }
            text.Append("}");
            return text.ToString();
        }

        private static void CollectNamespaces(ITypeSymbol typeSymbol, ISet<string> namespaces)
        {
            var str = typeSymbol.ToDisplayString();
            var matches = Regex.Matches(str, @"([\w\.]+)\.\w+");
            foreach (Match match in matches)
            {
                namespaces.Add(match.Groups[1].Value);
            }
        }

        private static string ToMinimalDisplayString(this ISymbol symbol)
        {
            return symbol.ToDisplayString(SymbolDisplayFormat.MinimallyQualifiedFormat);
        }
    }
}
