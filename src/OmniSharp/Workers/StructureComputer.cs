using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using OmniSharp.Models;

namespace OmniSharp
{
    public class StructureComputer : CSharpSyntaxWalker
    {
        public static async Task<IEnumerable<Node>> Compute(IEnumerable<Document> documents)
        {
            var root = new Node() { ChildNodes = new List<Node>() };
            var visitor = new StructureComputer(root);
            foreach (var document in documents)
            {
                ((CSharpSyntaxNode)await document.GetSyntaxRootAsync()).Accept(visitor);
            }
            return root.ChildNodes;
        }

        private readonly Stack<Node> _roots = new Stack<Node>();

        private StructureComputer(Node root)
        {
            _roots.Push(root);
        }

        private Node AsNode(SyntaxNode node, string text, Location location)
        {
            var ret = new Node();
            var lineSpan = location.GetLineSpan();
            ret.ChildNodes = new List<Node>();
            ret.Kind = node.CSharpKind().ToString();
            ret.Location = new QuickFix();
            ret.Location.Text = text;
            ret.Location.Line = 1 + lineSpan.StartLinePosition.Line;
            ret.Location.Column = 1 + lineSpan.StartLinePosition.Character;
            ret.Location.EndLine = 1 + lineSpan.EndLinePosition.Line;
            ret.Location.EndColumn = 1 + lineSpan.EndLinePosition.Character;
            return ret;
        }

        private Node AsChild(SyntaxNode node, string text, Location location)
        {
            var child = AsNode(node, text, location);
            var childNodes = ((List<Node>)_roots.Peek().ChildNodes);
            // Prevent inserting the same node multiple times
            // but make sure to insert them at the right spot
            var idx = childNodes.BinarySearch(child);
            if (idx < 0)
            {
                childNodes.Insert(~idx, child);
                return child;
            }
            else
            {
                return childNodes[idx];
            }
        }

        private Node AsParent(SyntaxNode node, string text, Action fn, Location location)
        {
            var child = AsChild(node, text, location);
            _roots.Push(child);
            fn();
            _roots.Pop();
            return child;
        }

        public override void VisitClassDeclaration(ClassDeclarationSyntax node)
        {
            AsParent(node, node.Identifier.Text, () => base.VisitClassDeclaration(node), node.Identifier.GetLocation());
        }

        public override void VisitInterfaceDeclaration(InterfaceDeclarationSyntax node)
        {
            AsParent(node, node.Identifier.Text, () => base.VisitInterfaceDeclaration(node), node.Identifier.GetLocation());
        }

        public override void VisitEnumDeclaration(EnumDeclarationSyntax node)
        {
            AsParent(node, node.Identifier.Text, () => base.VisitEnumDeclaration(node), node.Identifier.GetLocation());
        }

        public override void VisitEnumMemberDeclaration(EnumMemberDeclarationSyntax node)
        {
            AsChild(node, node.Identifier.Text, node.Identifier.GetLocation());
        }

        public override void VisitPropertyDeclaration(PropertyDeclarationSyntax node)
        {
            AsChild(node, node.Identifier.Text, node.Identifier.GetLocation());
        }

        public override void VisitMethodDeclaration(MethodDeclarationSyntax node)
        {
            AsChild(node, node.Identifier.Text, node.Identifier.GetLocation());
        }

        public override void VisitFieldDeclaration(FieldDeclarationSyntax node)
        {
            AsChild(node, node.Declaration.Variables.First().Identifier.Text, node.Declaration.Variables.First().Identifier.GetLocation());
        }

        public override void VisitEventFieldDeclaration(EventFieldDeclarationSyntax node)
        {
            AsChild(node, node.Declaration.Variables.First().Identifier.Text, node.Declaration.Variables.First().Identifier.GetLocation());
        }

        public override void VisitStructDeclaration(StructDeclarationSyntax node)
        {
            AsParent(node, node.Identifier.Text, () => base.VisitStructDeclaration(node), node.Identifier.GetLocation());
        }

        public override void VisitConstructorDeclaration(ConstructorDeclarationSyntax node)
        {
            AsChild(node, node.Identifier.Text, node.Identifier.GetLocation());
        }
    }
}