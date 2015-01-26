using System;
using Microsoft.CodeAnalysis;
using OmniSharp.Models;

namespace OmniSharp
{
    public static class RoslynExtensions
    {
        public static QuickFix AsQuickFix(this Node node)
        {
            var quickFix = new QuickFix();
            quickFix.Text = node.Text;
            quickFix.Line = 1 + node.Location.GetLineSpan().StartLinePosition.Line;
            quickFix.Column = 1 + node.Location.GetLineSpan().StartLinePosition.Character;
            quickFix.EndLine = 1 + node.Location.GetLineSpan().EndLinePosition.Line;
            quickFix.EndColumn = 1 + node.Location.GetLineSpan().EndLinePosition.Character;

            return quickFix;
        }
    }
}