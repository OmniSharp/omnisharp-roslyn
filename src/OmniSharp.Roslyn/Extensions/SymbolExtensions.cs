using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Text;
using Microsoft.CodeAnalysis;
using OmniSharp.Models.V2;
using OmniSharp.Roslyn.Utilities;
using OmniSharp.Utilities;

namespace OmniSharp.Extensions
{
    public static class SymbolExtensions
    {
        private static readonly SymbolDisplayFormat NameFormat =
            new SymbolDisplayFormat(
                globalNamespaceStyle: SymbolDisplayGlobalNamespaceStyle.Omitted,
                typeQualificationStyle: SymbolDisplayTypeQualificationStyle.NameAndContainingTypesAndNamespaces,
                propertyStyle: SymbolDisplayPropertyStyle.NameOnly,
                genericsOptions: SymbolDisplayGenericsOptions.IncludeTypeParameters | SymbolDisplayGenericsOptions.IncludeVariance,
                memberOptions: SymbolDisplayMemberOptions.IncludeParameters | SymbolDisplayMemberOptions.IncludeExplicitInterface,
                parameterOptions:
                    SymbolDisplayParameterOptions.IncludeParamsRefOut |
                    SymbolDisplayParameterOptions.IncludeExtensionThis |
                    SymbolDisplayParameterOptions.IncludeType |
                    SymbolDisplayParameterOptions.IncludeName,
                miscellaneousOptions:
                    SymbolDisplayMiscellaneousOptions.EscapeKeywordIdentifiers |
                    SymbolDisplayMiscellaneousOptions.UseSpecialTypes);

        private readonly static CachedStringBuilder s_cachedBuilder;

        public static string ToNameDisplayString(this ISymbol symbol)
        {
            return symbol.ToDisplayString(NameFormat);
        }

        public static INamedTypeSymbol GetContainingTypeOrThis(this ISymbol symbol)
        {
            if (symbol is INamedTypeSymbol namedType)
            {
                return namedType;
            }

            return symbol.ContainingType;
        }

        public static string GetMetadataName(this ISymbol symbol)
        {
            if (symbol == null)
            {
                throw new ArgumentNullException(nameof(symbol));
            }

            var symbols = new Stack<ISymbol>();

            while (symbol != null)
            {
                if (symbol.Kind == SymbolKind.Assembly ||
                    symbol.Kind == SymbolKind.NetModule)
                {
                    break;
                }

                if ((symbol as INamespaceSymbol)?.IsGlobalNamespace == true)
                {
                    break;
                }

                symbols.Push(symbol);
                symbol = symbol.ContainingSymbol;
            }

            var builder = s_cachedBuilder.Acquire();
            try
            {
                ISymbol current = null, previous = null;

                while (symbols.Count > 0)
                {
                    current = symbols.Pop();

                    if (previous != null)
                    {
                        if (previous.Kind == SymbolKind.NamedType &&
                            current.Kind == SymbolKind.NamedType)
                        {
                            builder.Append('+');
                        }
                        else
                        {
                            builder.Append('.');
                        }
                    }

                    builder.Append(current.MetadataName);

                    previous = current;
                }

                return builder.ToString();
            }
            finally
            {
                s_cachedBuilder.Release(builder);
            }
        }

        public static string GetAccessibilityString(this ISymbol symbol)
            => symbol.DeclaredAccessibility switch
            {
                Accessibility.Public => SymbolAccessibilities.Public,
                Accessibility.Internal => SymbolAccessibilities.Internal,
                Accessibility.Private => SymbolAccessibilities.Private,
                Accessibility.Protected => SymbolAccessibilities.Protected,
                Accessibility.ProtectedOrInternal => SymbolAccessibilities.ProtectedInternal,
                Accessibility.ProtectedAndInternal => SymbolAccessibilities.PrivateProtected,
                _ => null,
            };

        public static string GetKindString(this ISymbol symbol)
            => symbol switch
            {
                INamespaceSymbol _ => SymbolKinds.Namespace,
                ITypeSymbol typeSymbol => typeSymbol.GetKindString(),
                IMethodSymbol methodSymbol => methodSymbol.GetKindString(),
                IFieldSymbol fieldSymbol => fieldSymbol.GetKindString(),
                IPropertySymbol propertySymbol => propertySymbol.GetKindString(),
                IEventSymbol _ => SymbolKinds.Event,
                IParameterSymbol _ => SymbolKinds.Parameter,
                _ => SymbolKinds.Unknown,
            };

        public static string GetKindString(this ITypeSymbol namedTypeSymbol)
            => namedTypeSymbol.TypeKind switch
            {
                TypeKind.Class => SymbolKinds.Class,
                TypeKind.Delegate => SymbolKinds.Delegate,
                TypeKind.Enum => SymbolKinds.Enum,
                TypeKind.Interface => SymbolKinds.Interface,
                TypeKind.Struct => SymbolKinds.Struct,
                TypeKind.Array => SymbolKinds.Array,
                TypeKind.TypeParameter => SymbolKinds.TypeParameter,
                _ => SymbolKinds.Unknown,
            };

        public static string GetKindString(this IMethodSymbol methodSymbol)
            => methodSymbol.MethodKind switch
            {
                MethodKind.Constructor or MethodKind.StaticConstructor => SymbolKinds.Constructor,
                MethodKind.Destructor => SymbolKinds.Destructor,
                MethodKind.Conversion or MethodKind.BuiltinOperator or MethodKind.UserDefinedOperator => SymbolKinds.Operator,
                _ => SymbolKinds.Method,
            };

        public static string GetKindString(this IFieldSymbol fieldSymbol)
        {
            if (fieldSymbol.ContainingType?.TypeKind == TypeKind.Enum &&
                fieldSymbol.HasConstantValue)
            {
                return SymbolKinds.EnumMember;
            }

            return fieldSymbol.IsConst
                ? SymbolKinds.Constant
                : SymbolKinds.Field;
        }

        public static string GetKindString(this IPropertySymbol propertySymbol)
            => propertySymbol.IsIndexer
                ? SymbolKinds.Indexer
                : SymbolKinds.Property;

        public static bool IsOverridable(this ISymbol symbol) => symbol?.ContainingType?.TypeKind == TypeKind.Class && !symbol.IsSealed && (symbol.IsVirtual || symbol.IsAbstract || symbol.IsOverride);

        /// <summary>
        /// Do not use this API in new OmniSharp endpoints. Use <see cref="GetKindString(ISymbol)"/> instead.
        /// </summary>
        [EditorBrowsable(EditorBrowsableState.Never)]
        internal static string GetKind(this ISymbol symbol)
        {
            if (symbol is INamedTypeSymbol namedType)
            {
                return Enum.GetName(namedType.TypeKind.GetType(), namedType.TypeKind);
            }

            if (symbol.Kind == SymbolKind.Field &&
                symbol.ContainingType?.TypeKind == TypeKind.Enum &&
                symbol.Name != WellKnownMemberNames.EnumBackingFieldName)
            {
                return "EnumMember";
            }

            if ((symbol as IFieldSymbol)?.IsConst == true)
            {
                return "Const";
            }

            return Enum.GetName(symbol.Kind.GetType(), symbol.Kind);
        }

        internal static INamedTypeSymbol GetTopLevelContainingNamedType(this ISymbol symbol)
        {
            // Traverse up until we find a named type that is parented by the namespace
            var topLevelNamedType = symbol;
            while (!SymbolEqualityComparer.Default.Equals(topLevelNamedType.ContainingSymbol, symbol.ContainingNamespace) ||
                topLevelNamedType.Kind != SymbolKind.NamedType)
            {
                topLevelNamedType = topLevelNamedType.ContainingSymbol;
            }

            return (INamedTypeSymbol)topLevelNamedType;
        }

        public static string GetSymbolName(this ISymbol symbol)
        {
            var topLevelSymbol = symbol.GetTopLevelContainingNamedType();
            return GetTypeDisplayString(topLevelSymbol);
        }

        private static string GetTypeDisplayString(INamedTypeSymbol symbol)
        {
            if (symbol.SpecialType != SpecialType.None)
            {
                var specialType = symbol.SpecialType;
                var name = Enum.GetName(typeof(SpecialType), symbol.SpecialType).Replace("_", ".");
                return name;
            }

            if (symbol.IsGenericType)
            {
                symbol = symbol.ConstructUnboundGenericType();
            }

            if (symbol.IsUnboundGenericType)
            {
                // TODO: Is this the best to get the fully metadata name?
                var parts = symbol.ToDisplayParts();
                var filteredParts = parts.Where(x => x.Kind != SymbolDisplayPartKind.Punctuation).ToArray();
                var typeName = new StringBuilder();
                foreach (var part in filteredParts.Take(filteredParts.Length - 1))
                {
                    typeName.Append(part.Symbol.Name);
                    typeName.Append(".");
                }
                typeName.Append(symbol.MetadataName);

                return typeName.ToString();
            }

            return symbol.ToDisplayString();
        }

        internal static string GetFilePathForExternalSymbol(this ISymbol symbol, Project project)
        {
            var topLevelSymbol = symbol.GetTopLevelContainingNamedType();
            return $"$metadata$/Project/{Folderize(project.Name)}/Assembly/{Folderize(topLevelSymbol.ContainingAssembly.Name)}/Symbol/{Folderize(GetTypeDisplayString(topLevelSymbol))}.cs".Replace(Path.AltDirectorySeparatorChar, Path.DirectorySeparatorChar);
        }

        private static string Folderize(string path) => string.Join("/", path.Split('.'));

        public static bool IsInterfaceType(this ISymbol symbol) => (symbol as ITypeSymbol)?.IsInterfaceType() == true;

        public static bool IsInterfaceType(this ITypeSymbol symbol) => symbol?.TypeKind == TypeKind.Interface;

        public static bool IsImplementableMember(this ISymbol symbol)
        {
            if (symbol?.ContainingType?.TypeKind == TypeKind.Interface)
            {
                if (symbol.Kind == SymbolKind.Event)
                {
                    return true;
                }

                if (symbol.Kind == SymbolKind.Property)
                {
                    return true;
                }

                if (symbol.Kind == SymbolKind.Method)
                {
                    var methodSymbol = (IMethodSymbol)symbol;
                    if (methodSymbol.MethodKind == MethodKind.Ordinary ||
                        methodSymbol.MethodKind == MethodKind.PropertyGet ||
                        methodSymbol.MethodKind == MethodKind.PropertySet)
                    {
                        return true;
                    }
                }
            }

            return false;
        }
    }
}
