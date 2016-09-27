using System;
using System.Collections.Generic;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace OmniSharp.Tests
{
    internal class UsingComparer : IComparer<UsingDirectiveSyntax>
    {
        public static UsingComparer Instance { get; } = new UsingComparer();

        public int Compare(UsingDirectiveSyntax using1, UsingDirectiveSyntax using2)
        {
            if (using1 == using2)
            {
                return 0;
            }

            var usingNamespace1 = using1 != null && using1.Alias == null && !using1.StaticKeyword.IsKind(SyntaxKind.StaticKeyword);
            var usingNamespace2 = using2 != null && using2.Alias == null && !using2.StaticKeyword.IsKind(SyntaxKind.StaticKeyword);

            var usingStatic1 = using1 != null && using1.StaticKeyword.IsKind(SyntaxKind.StaticKeyword);
            var usingStatic2 = using2 != null && using2.StaticKeyword.IsKind(SyntaxKind.StaticKeyword);

            var usingAlias1 = using1 != null && using1.Alias != null;
            var usingAlias2 = using2 != null && using2.Alias != null;

            if (usingNamespace1 && !usingNamespace2)
            {
                return -1;
            }
            else if (usingNamespace2 && !usingNamespace1)
            {
                return 1;
            }
            else if (usingStatic1 && !usingStatic2)
            {
                return -1;
            }
            else if (usingStatic2 && !usingStatic1)
            {
                return 1;
            }
            else if (usingAlias1 && !usingAlias2)
            {
                return -1;
            }
            else if (usingAlias2 && !usingAlias1)
            {
                return -1;
            }

            if (usingAlias1)
            {
                var aliasComparisonResult = StringComparer.CurrentCulture.Compare(using1.Alias.ToString(), using2.Alias.ToString());

                if (aliasComparisonResult != 0)
                {
                    return aliasComparisonResult;
                }
            }

            return CompareNames(using1.Name, using2.Name);
        }

        private static int CompareNames(NameSyntax name1, NameSyntax name2)
        {
            var nameText1 = name1.ToString();
            var nameText2 = name2.ToString();

            var systemNameText1 = nameText1.StartsWith("System");
            var systemNameText2 = nameText2.StartsWith("System");

            if (systemNameText1 && !systemNameText2)
            {
                return -1;
            }
            else if (systemNameText2 && !systemNameText1)
            {
                return 1;
            }

            return StringComparer.CurrentCulture.Compare(nameText1, nameText2);
        }
    }
}
