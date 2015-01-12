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

        private Node AsNode(SyntaxNode node, string text)
        {
            var ret = new Node();
            ret.ChildNodes = new List<Node>();
            ret.Kind = node.CSharpKind().ToString();
            ret.Location = new QuickFix();
            ret.Location.Text = text;
            ret.Location.Line = 1 + node.GetLocation().GetLineSpan().StartLinePosition.Line;
            ret.Location.Column = 1 + node.GetLocation().GetLineSpan().StartLinePosition.Character;
            ret.Location.EndLine = 1 + node.GetLocation().GetLineSpan().EndLinePosition.Line;
            ret.Location.EndColumn = 1 + node.GetLocation().GetLineSpan().EndLinePosition.Character;
            return ret;
        }

        private Node AsChild(SyntaxNode node, string text)
        {
            var child = AsNode(node, text);
            var parent = _roots.Peek();
            var newChildNodes = new List<Node>();
            newChildNodes.AddRange(parent.ChildNodes);
            newChildNodes.Add(child);
            parent.ChildNodes = newChildNodes;
            return child;
        }

        private Node AsParent(SyntaxNode node, string text, Action fn)
        {
            var child = AsChild(node, text);
            _roots.Push(child);
            fn();
            _roots.Pop();
            return child;
        }

        public override void VisitClassDeclaration(ClassDeclarationSyntax node)
        {
            AsParent(node, node.Identifier.Text, () => base.VisitClassDeclaration(node));
        }

        public override void VisitInterfaceDeclaration(InterfaceDeclarationSyntax node)
        {
            AsParent(node, node.Identifier.Text, () => base.VisitInterfaceDeclaration(node));
        }

        public override void VisitEnumDeclaration(EnumDeclarationSyntax node)
        {
            AsParent(node, node.Identifier.Text, () => base.VisitEnumDeclaration(node));
        }

        public override void VisitEnumMemberDeclaration(EnumMemberDeclarationSyntax node)
        {
            AsChild(node, node.Identifier.Text);
        }

        public override void VisitPropertyDeclaration(PropertyDeclarationSyntax node)
        {
            AsChild(node, node.Identifier.Text);
        }

        public override void VisitMethodDeclaration(MethodDeclarationSyntax node)
        {
            AsChild(node, node.Identifier.Text);
        }

        public override void VisitFieldDeclaration(FieldDeclarationSyntax node)
        {
            AsChild(node, node.Declaration.Variables.First().Identifier.Text);
        }
        
        public override void VisitEventFieldDeclaration(EventFieldDeclarationSyntax node) 
        {
            AsChild(node, node.Declaration.Variables.First().Identifier.Text);
        }

        public override void VisitStructDeclaration(StructDeclarationSyntax node)
        {
            AsParent(node, node.Identifier.Text, () => base.VisitStructDeclaration(node));
        }
    }
}