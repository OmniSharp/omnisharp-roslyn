using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Text;
using OmniSharp.Abstractions.Services;
using OmniSharp.Models;
using OmniSharp.Models.MembersTree;

namespace OmniSharp
{
    public class StructureComputer : CSharpSyntaxWalker
    {
        private readonly Stack<FileMemberElement> _roots = new Stack<FileMemberElement>();
        private readonly IEnumerable<ISyntaxFeaturesDiscover> _featureDiscovers;
        private string _currentProject;
        private SemanticModel _semanticModel;

        public static async Task<IEnumerable<FileMemberElement>> Compute(
            IEnumerable<Document> documents,
            IEnumerable<ISyntaxFeaturesDiscover> featureDiscovers)
        {
            var root = new FileMemberElement() { ChildNodes = new List<FileMemberElement>() };
            var visitor = new StructureComputer(root, featureDiscovers);

            foreach (var document in documents)
            {
                await visitor.Process(document);
            }

            return root.ChildNodes;
        }

        public static Task<IEnumerable<FileMemberElement>> Compute(IEnumerable<Document> documents) 
            => Compute(documents, Enumerable.Empty<ISyntaxFeaturesDiscover>());

        private StructureComputer(FileMemberElement root, IEnumerable<ISyntaxFeaturesDiscover> featureDiscovers)
        {
            _featureDiscovers = featureDiscovers ?? Enumerable.Empty<ISyntaxFeaturesDiscover>();
            _roots.Push(root);
        }

        private async Task Process(Document document)
        {
            _currentProject = document.Project.Name;

            if (_featureDiscovers.Any(dis => dis.NeedSemanticModel))
            {
                _semanticModel = await document.GetSemanticModelAsync();
            }

            var syntaxRoot = await document.GetSyntaxRootAsync();
            (syntaxRoot as CSharpSyntaxNode)?.Accept(this);
        }

        private FileMemberElement AsNode(SyntaxNode node, string text, Location location, TextSpan attributeSpan)
        {
            var ret = new FileMemberElement();
            var lineSpan = location.GetLineSpan();
            ret.Projects = new List<string>();
            ret.ChildNodes = new List<FileMemberElement>();
            ret.Kind = node.Kind().ToString();
            ret.AttributeSpanStart = attributeSpan.Start;
            ret.AttributeSpanEnd = attributeSpan.End;
            ret.Location = new QuickFix()
            {
                Text = text,
                FileName = lineSpan.Path,
                Line = lineSpan.StartLinePosition.Line,
                Column = lineSpan.StartLinePosition.Character,
                EndLine = lineSpan.EndLinePosition.Line,
                EndColumn = lineSpan.EndLinePosition.Character
            };

            foreach (var featureDiscover in _featureDiscovers)
            {
                var features = featureDiscover.Discover(node, _semanticModel);
                foreach (var feature in features)
                {
                    ret.Features.Add(feature);
                }
            }

            return ret;
        }

        private FileMemberElement AsChild(SyntaxNode node, string text, Location location, TextSpan attributeSpan)
        {
            var child = AsNode(node, text, location, attributeSpan);
            var childNodes = ((List<FileMemberElement>)_roots.Peek().ChildNodes);

            // Prevent inserting the same node multiple times
            // but make sure to insert them at the right spot
            var idx = childNodes.BinarySearch(child);
            if (idx < 0)
            {
                ((List<string>)child.Projects).Add(_currentProject);
                childNodes.Insert(~idx, child);
                return child;
            }
            else
            {
                ((List<string>)childNodes[idx].Projects).Add(_currentProject);
                return childNodes[idx];
            }
        }

        private FileMemberElement AsParent(SyntaxNode node, string text, Action fn, Location location, TextSpan attributeSpan)
        {
            var child = AsChild(node, text, location, attributeSpan);
            _roots.Push(child);
            fn();
            _roots.Pop();
            return child;
        }

        public override void VisitClassDeclaration(ClassDeclarationSyntax node)
        {
            AsParent(node, node.Identifier.Text, () => base.VisitClassDeclaration(node), node.Identifier.GetLocation(), node.AttributeLists.Span);
        }

        public override void VisitInterfaceDeclaration(InterfaceDeclarationSyntax node)
        {
            AsParent(node, node.Identifier.Text, () => base.VisitInterfaceDeclaration(node), node.Identifier.GetLocation(), node.AttributeLists.Span);
        }

        public override void VisitEnumDeclaration(EnumDeclarationSyntax node)
        {
            AsParent(node, node.Identifier.Text, () => base.VisitEnumDeclaration(node), node.Identifier.GetLocation(), node.AttributeLists.Span);
        }

        public override void VisitEnumMemberDeclaration(EnumMemberDeclarationSyntax node)
        {
            AsChild(node, node.Identifier.Text, node.Identifier.GetLocation(), node.AttributeLists.Span);
        }

        public override void VisitPropertyDeclaration(PropertyDeclarationSyntax node)
        {
            AsChild(node, node.Identifier.Text, node.Identifier.GetLocation(), node.AttributeLists.Span);
        }

        public override void VisitMethodDeclaration(MethodDeclarationSyntax node)
        {
            AsChild(node, node.Identifier.Text, node.Identifier.GetLocation(), node.AttributeLists.Span);
        }

        public override void VisitFieldDeclaration(FieldDeclarationSyntax node)
        {
            AsChild(node, node.Declaration.Variables.First().Identifier.Text, node.Declaration.Variables.First().Identifier.GetLocation(), node.AttributeLists.Span);
        }

        public override void VisitEventFieldDeclaration(EventFieldDeclarationSyntax node)
        {
            AsChild(node, node.Declaration.Variables.First().Identifier.Text, node.Declaration.Variables.First().Identifier.GetLocation(), node.AttributeLists.Span);
        }

        public override void VisitStructDeclaration(StructDeclarationSyntax node)
        {
            AsParent(node, node.Identifier.Text, () => base.VisitStructDeclaration(node), node.Identifier.GetLocation(), node.AttributeLists.Span);
        }

        public override void VisitConstructorDeclaration(ConstructorDeclarationSyntax node)
        {
            AsChild(node, node.Identifier.Text, node.Identifier.GetLocation(), node.AttributeLists.Span);
        }
    }
}
