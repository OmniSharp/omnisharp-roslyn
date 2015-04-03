using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.FindSymbols;
using OmniSharp.Models;

namespace OmniSharp
{
        //TODO:
        //1. Remove unused using statements
        //2. Sort using statements
        //3. Remove redundant using statements
        //4. Replace code which checks accessibility by Roslyn method if it is made available
    public class FixUsingsWorker
    {
        public static async Task<FixUsingsResponse> AddMissingUsings(string fileName, Document document, SemanticModel semanticModel)
        {
            var compilationUnitSyntax = await AddUnambiguousUsings(fileName, document, semanticModel);
            document = document.WithSyntaxRoot(compilationUnitSyntax);
            semanticModel = await document.GetSemanticModelAsync();
            return await GetAmbiguousUsings(fileName, document, semanticModel, compilationUnitSyntax);
        }

        private static async Task<CompilationUnitSyntax> AddUnambiguousUsings(string fileName, Document document, SemanticModel semanticModel)
        {
            var root = (await document.GetSyntaxTreeAsync()).GetRoot();
            var unresolvedTypes = root.DescendantNodes()
                .OfType<SimpleNameSyntax>()
                .Where(x => semanticModel.GetSymbolInfo(x).Symbol == null &&
                        semanticModel.GetSymbolInfo(x).CandidateReason != CandidateReason.OverloadResolutionFailure);
            var compilationUnitSyntax = (CompilationUnitSyntax)root;
            var usingsToAdd = new HashSet<string>();
            var unresolvedSet = new HashSet<string>();
            var candidateUsings = new List<ISymbol>();

            if (compilationUnitSyntax != null)
            {
                usingsToAdd.UnionWith(GetUsings(root));

                foreach (var unresolvedType in unresolvedTypes)
                {
                    candidateUsings = GetCandidateUsings(document, unresolvedType);
                    //If there is only one candidate - add it to the usings list
                    if (candidateUsings.Count() == 1)
                    {
                        var candidateNameSpace = candidateUsings[0].ContainingNamespace.ToString();
                        var candidateName = SyntaxFactory.ParseName(candidateNameSpace);
                        var usingToAdd = SyntaxFactory.UsingDirective(candidateName).NormalizeWhitespace()
                            .WithTrailingTrivia(SyntaxFactory.Whitespace(Environment.NewLine));
                        var usingToAddText = usingToAdd.GetText().ToString();

                        if (!usingsToAdd.Contains(usingToAddText.Trim()))
                        {
                            compilationUnitSyntax = compilationUnitSyntax.AddUsings(usingToAdd);
                            usingsToAdd.Add(usingToAddText.Trim());
                        }
                    }
                }
            }

            return compilationUnitSyntax;
        }


        private static async Task<FixUsingsResponse> GetAmbiguousUsings(string fileName, Document document, SemanticModel semanticModel, CompilationUnitSyntax compilationUnitSyntax)
        {
            var root = (await document.GetSyntaxTreeAsync()).GetRoot();
            var unresolvedTypes = root.DescendantNodes()
                .OfType<SimpleNameSyntax>()
                .Where(x => semanticModel.GetSymbolInfo(x).Symbol == null &&
                        semanticModel.GetSymbolInfo(x).CandidateReason != CandidateReason.OverloadResolutionFailure);
            var usingsToAdd = new HashSet<string>();
            var unresolvedSet = new HashSet<string>();
            var candidateUsings = new List<ISymbol>();
            var ambiguous = new List<QuickFix>();

            if (compilationUnitSyntax != null)
            {
                usingsToAdd.UnionWith(GetUsings(root));

                foreach (var unresolvedType in unresolvedTypes)
                {
                    candidateUsings = GetCandidateUsings(document, unresolvedType);

                    //Set the symbol as an ambiguous match
                    foreach (var candidateUsing in candidateUsings)
                    {
                        var unresolvedText = unresolvedType.Identifier.ValueText;

                        if (!unresolvedSet.Contains(unresolvedText))
                        {
                            var unresolvedLocation = unresolvedType.GetLocation().GetLineSpan().StartLinePosition;
                            ambiguous.Add(new QuickFix
                            {
                                Line = unresolvedLocation.Line + 1,
                                Column = unresolvedLocation.Character + 1,
                                FileName = fileName,
                                Text = "`" + unresolvedText + "`" + " is ambiguous"
                            });
                            unresolvedSet.Add(unresolvedText);
                        }
                    }
                }
            }

            return new FixUsingsResponse(compilationUnitSyntax.GetText().ToString(), ambiguous);
        }


        private static HashSet<string> GetUsings(SyntaxNode root)
        {
            var usings = new HashSet<string>();
            root.DescendantNodes().OfType<UsingDirectiveSyntax>()
                .ToList().ForEach(x =>
                        {
                            if (!usings.Contains(x.ToString().Trim()))
                            {
                                usings.Add(x.ToString().Trim());
                            }
                        });
            return usings;
        }

        private static List<ISymbol> GetCandidateUsings(Document document, SimpleNameSyntax unresolvedType)
        {
            var candidateUsings = new List<ISymbol>();
            //Get all candidate usings by type
            var candidateUsingsTypes = SymbolFinder.FindDeclarationsAsync(document.Project, unresolvedType.Identifier.ValueText, false, SymbolFilter.Type, CancellationToken.None).Result.Where(c => c.DeclaredAccessibility == Accessibility.Public);
            var candidateUsingsAll = candidateUsingsTypes
                .Where(s => HasValidContainer(s) && !IsEnumMember(s))
                .ToList();

            //Get all candidate usings by member
            var candidateUsingsMembers = SymbolFinder.FindDeclarationsAsync(document.Project, unresolvedType.Identifier.ValueText, false, SymbolFilter.Member, CancellationToken.None).Result.Where(c => c.DeclaredAccessibility == Accessibility.Public);
            candidateUsingsAll.AddRange(candidateUsingsMembers
                .Where(s => !IsEnumMember(s))
                .ToList());

            //Dedup candidate usings and handle Linq separately
            foreach (var candidateUsing in candidateUsingsAll)
            {
                if (!candidateUsings.Any(c => c.ContainingNamespace.ToString() == candidateUsing.ContainingNamespace.ToString()))
                {
                    if (candidateUsing.ContainingNamespace.ToString() == "System.Linq")
                    {
                        candidateUsings.Clear();
                        candidateUsings.Add(candidateUsing);
                        break;
                    }
                    else
                    {
                        candidateUsings.Add(candidateUsing);
                    }
                }
            }

            return candidateUsings;
        }

        private static bool HasValidContainer(ISymbol symbol)
        {
            var container = symbol.ContainingSymbol;
            return container is INamespaceSymbol ||
                (container is INamedTypeSymbol && !((INamedTypeSymbol)container).IsGenericType);
        }

        private static bool IsEnumMember(ISymbol symbol)
        {
            var containingType = symbol.ContainingType;
            return containingType != null && containingType.TypeKind == TypeKind.Enum;
        }

    }
}
