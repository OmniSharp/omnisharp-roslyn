using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using OmniSharp.Models;

namespace OmniSharp
{
    public class StructureComputer : CSharpSyntaxWalker
    {
        private readonly Stack<FileMemberElement> _roots = new Stack<FileMemberElement>();
        private string _currentProject;
        private SemanticModel _semanticModel;

        public static async Task<IEnumerable<FileMemberElement>> Compute(IEnumerable<Document> documents)
        {
            var root = new FileMemberElement() { ChildNodes = new List<FileMemberElement>() };
            var visitor = new StructureComputer(root);

            foreach (var document in documents)
            {
                await visitor.Process(document);
            }

            return root.ChildNodes;
        }

        private StructureComputer(FileMemberElement root)
        {
            _roots.Push(root);
        }

        private async Task Process(Document document)
        {
            _currentProject = document.Project.Name;
            _semanticModel = await document.GetSemanticModelAsync();

            var syntaxRoot = await document.GetSyntaxRootAsync();
            (syntaxRoot as CSharpSyntaxNode)?.Accept(this);
        }

        private FileMemberElement AsNode(SyntaxNode node, string text, Location location, params string[] features)
        {
            var ret = new FileMemberElement();
            var lineSpan = location.GetLineSpan();
            ret.Projects = new List<string>();
            ret.ChildNodes = new List<FileMemberElement>();
            ret.Kind = node.Kind().ToString();
            ret.Location = new QuickFix();
            ret.Location.Text = text;
            ret.Location.Line = lineSpan.StartLinePosition.Line;
            ret.Location.Column = lineSpan.StartLinePosition.Character;
            ret.Location.EndLine = lineSpan.EndLinePosition.Line;
            ret.Location.EndColumn = lineSpan.EndLinePosition.Character;

            foreach (var feature in features)
            {
                ret.Features.Add(feature);
            }

            return ret;
        }

        private FileMemberElement AsChild(SyntaxNode node, string text, Location location, params string[] features)
        {
            var child = AsNode(node, text, location, features);
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

        private FileMemberElement AsParent(SyntaxNode node, string text, Action fn, Location location)
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
            if (XunitTestMethodHelper.IsTestMethod(node, _semanticModel))
            {
                var methodName = _semanticModel.GetDeclaredSymbol(node).ToDisplayString();
                methodName = methodName.Substring(0, methodName.IndexOf('('));
                
                AsChild(node, node.Identifier.Text, node.Identifier.GetLocation(), $"XunitTestMethod:{methodName}");
            }
            else
            {
                AsChild(node, node.Identifier.Text, node.Identifier.GetLocation());
            }
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

    class XunitTestMethodHelper
    {
        public static bool IsTestMethod(
            MethodDeclarationSyntax node,
            SemanticModel sematicModel)
        {
            return node.DescendantNodes()
                       .OfType<AttributeSyntax>()
                       .Select(attr => sematicModel.GetTypeInfo(attr).Type)
                       .Any(IsDerivedFromFactAttribute);
        }

        private static bool IsDerivedFromFactAttribute(ITypeSymbol symbol)
        {
            string fullName;
            do
            {
                fullName = $"{symbol.ContainingNamespace}.{symbol.Name}";
                if (fullName == "Xunit.FactAttribute")
                {
                    return true;
                }

                symbol = symbol.BaseType;
            } while (symbol.Name != "Object");

            return false;
        }
    }
}