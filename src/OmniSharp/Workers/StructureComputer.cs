using System;
using System.Collections.Generic;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using OmniSharp.Models;

namespace OmniSharp
{
    public class StructureComputer : CSharpSyntaxWalker
    {
        public static IEnumerable<Node> Compute(CSharpSyntaxNode node)
        {
            var root = new Node() { ChildNodes = new List<Node>() };
            new StructureComputer(node, root);
            return root.ChildNodes;
        }

        private readonly Stack<Node> _roots = new Stack<Node>();

        private StructureComputer(CSharpSyntaxNode node, Node root)
        {
            _roots.Push(root);
            node.Accept(this);
        }

        private Node AsNode(SyntaxNode node, string text, Location location)
        {
            var ret = new Node();
            ret.ChildNodes = new List<Node>();
            ret.Kind = node.CSharpKind().ToString();
            ret.Location = new QuickFix();
            ret.Location.Text = text;
            if (location == null)
            {
                ret.Location.Line = 1 + node.GetLocation().GetLineSpan().StartLinePosition.Line;
                ret.Location.Column = 1 + node.GetLocation().GetLineSpan().StartLinePosition.Character;
                ret.Location.EndLine = 1 + node.GetLocation().GetLineSpan().EndLinePosition.Line;
                ret.Location.EndColumn = 1 + node.GetLocation().GetLineSpan().EndLinePosition.Character;
            }
            else
            {
                ret.Location.Line = location.GetLineSpan().StartLinePosition.Line;
                ret.Location.Column = 1 + location.GetLineSpan().StartLinePosition.Character;
                ret.Location.EndLine = 1 + location.GetLineSpan().EndLinePosition.Line;
                ret.Location.EndColumn = 1 + location.GetLineSpan().EndLinePosition.Character;
            }
            return ret;
        }

        private Node AsChild(SyntaxNode node, string text, Location location = null)
        {
            var child = AsNode(node, text, location);
            var parent = _roots.Peek();
            var newChildNodes = new List<Node>();
            newChildNodes.AddRange(parent.ChildNodes);
            newChildNodes.Add(child);
            parent.ChildNodes = newChildNodes;
            return child;
        }

        private Node AsParent(SyntaxNode node, string text, Action fn, Location location = null)
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