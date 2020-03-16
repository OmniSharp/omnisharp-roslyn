using System.Collections.Generic;
using System.Composition;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Classification;
using Microsoft.CodeAnalysis.Text;
using OmniSharp.Mef;
using OmniSharp.Models.SemanticHighlight;

namespace OmniSharp.Roslyn.CSharp.Services.SemanticHighlight
{
    [OmniSharpHandler(OmniSharpEndpoints.V2.Highlight, LanguageNames.CSharp)]
    public class SemanticHighlightService : IRequestHandler<SemanticHighlightRequest, SemanticHighlightResponse>
    {
        [ImportingConstructor]
        public SemanticHighlightService(OmniSharpWorkspace workspace)
        {
            _workspace = workspace;
        }

        public async Task<SemanticHighlightResponse> Handle(SemanticHighlightRequest request)
        {
            var documents = _workspace.GetDocuments(request.FileName);

            var results = new List<ClassifiedResult>();

            foreach (var document in documents)
            {
                var project = document.Project.Name;
                var text = await document.GetTextAsync();

                TextSpan textSpan;
                if (request.Range is object)
                {
                    var start = text.Lines.GetPosition(new LinePosition(request.Range.Start.Line, request.Range.Start.Column));
                    var end = text.Lines.GetPosition(new LinePosition(request.Range.End.Line, request.Range.End.Column));
                    textSpan = new TextSpan(start, end - start);
                }
                else
                {
                    textSpan = new TextSpan(0, text.Length);
                }

                results.AddRange((await Classifier.GetClassifiedSpansAsync(document, textSpan))
                    .Select(span => new ClassifiedResult()
                    {
                        Span = span,
                        Lines = text.Lines,
                    }));
            }

            return new SemanticHighlightResponse()
            {
                Spans = results
                    .GroupBy(result => result.Span.TextSpan.ToString())
                    .Select(grouping => CreateSemanticSpan(grouping, grouping.First().Lines))
                    .ToArray()
            };
        }

        private static SemanticHighlightSpan CreateSemanticSpan(IEnumerable<ClassifiedResult> results, TextLineCollection lines)
        {
            var additiveResults = results.Where(result => ClassificationTypeNames.AdditiveTypeNames.Contains(result.Span.ClassificationType));
            var modifiers = additiveResults.Select(result => _modifierMap[result.Span.ClassificationType]).ToArray();

            var result = results.Except(additiveResults).Single();
            var span = result.Span;

            var linePos = lines.GetLinePositionSpan(span.TextSpan);

            return new SemanticHighlightSpan
            {
                StartLine = linePos.Start.Line,
                EndLine = linePos.End.Line,
                StartColumn = linePos.Start.Character,
                EndColumn = linePos.End.Character,
                Type = _classificationMap[span.ClassificationType],
                Modifiers = modifiers
            };
        }

        class ClassifiedResult
        {
            public ClassifiedSpan Span { get; set; }
            public TextLineCollection Lines { get; set; }
        }

        private static readonly Dictionary<string, SemanticHighlightClassification> _classificationMap =
            new Dictionary<string, SemanticHighlightClassification>
            {
                [ClassificationTypeNames.Comment] = SemanticHighlightClassification.Comment,
                [ClassificationTypeNames.ExcludedCode] = SemanticHighlightClassification.ExcludedCode,
                [ClassificationTypeNames.Identifier] = SemanticHighlightClassification.Identifier,
                [ClassificationTypeNames.Keyword] = SemanticHighlightClassification.Keyword,
                [ClassificationTypeNames.ControlKeyword] = SemanticHighlightClassification.ControlKeyword,
                [ClassificationTypeNames.NumericLiteral] = SemanticHighlightClassification.NumericLiteral,
                [ClassificationTypeNames.Operator] = SemanticHighlightClassification.Operator,
                [ClassificationTypeNames.OperatorOverloaded] = SemanticHighlightClassification.OperatorOverloaded,
                [ClassificationTypeNames.PreprocessorKeyword] = SemanticHighlightClassification.PreprocessorKeyword,
                [ClassificationTypeNames.StringLiteral] = SemanticHighlightClassification.StringLiteral,
                [ClassificationTypeNames.WhiteSpace] = SemanticHighlightClassification.WhiteSpace,
                [ClassificationTypeNames.Text] = SemanticHighlightClassification.Text,
                [ClassificationTypeNames.StaticSymbol] = SemanticHighlightClassification.StaticSymbol,
                [ClassificationTypeNames.PreprocessorText] = SemanticHighlightClassification.PreprocessorText,
                [ClassificationTypeNames.Punctuation] = SemanticHighlightClassification.Punctuation,
                [ClassificationTypeNames.VerbatimStringLiteral] = SemanticHighlightClassification.VerbatimStringLiteral,
                [ClassificationTypeNames.StringEscapeCharacter] = SemanticHighlightClassification.StringEscapeCharacter,
                [ClassificationTypeNames.ClassName] = SemanticHighlightClassification.ClassName,
                [ClassificationTypeNames.DelegateName] = SemanticHighlightClassification.DelegateName,
                [ClassificationTypeNames.EnumName] = SemanticHighlightClassification.EnumName,
                [ClassificationTypeNames.InterfaceName] = SemanticHighlightClassification.InterfaceName,
                [ClassificationTypeNames.ModuleName] = SemanticHighlightClassification.ModuleName,
                [ClassificationTypeNames.StructName] = SemanticHighlightClassification.StructName,
                [ClassificationTypeNames.TypeParameterName] = SemanticHighlightClassification.TypeParameterName,
                [ClassificationTypeNames.FieldName] = SemanticHighlightClassification.FieldName,
                [ClassificationTypeNames.EnumMemberName] = SemanticHighlightClassification.EnumMemberName,
                [ClassificationTypeNames.ConstantName] = SemanticHighlightClassification.ConstantName,
                [ClassificationTypeNames.LocalName] = SemanticHighlightClassification.LocalName,
                [ClassificationTypeNames.ParameterName] = SemanticHighlightClassification.ParameterName,
                [ClassificationTypeNames.MethodName] = SemanticHighlightClassification.MethodName,
                [ClassificationTypeNames.ExtensionMethodName] = SemanticHighlightClassification.ExtensionMethodName,
                [ClassificationTypeNames.PropertyName] = SemanticHighlightClassification.PropertyName,
                [ClassificationTypeNames.EventName] = SemanticHighlightClassification.EventName,
                [ClassificationTypeNames.NamespaceName] = SemanticHighlightClassification.NamespaceName,
                [ClassificationTypeNames.LabelName] = SemanticHighlightClassification.LabelName,
                [ClassificationTypeNames.XmlDocCommentAttributeName] = SemanticHighlightClassification.XmlDocCommentAttributeName,
                [ClassificationTypeNames.XmlDocCommentAttributeQuotes] = SemanticHighlightClassification.XmlDocCommentAttributeQuotes,
                [ClassificationTypeNames.XmlDocCommentAttributeValue] = SemanticHighlightClassification.XmlDocCommentAttributeValue,
                [ClassificationTypeNames.XmlDocCommentCDataSection] = SemanticHighlightClassification.XmlDocCommentCDataSection,
                [ClassificationTypeNames.XmlDocCommentComment] = SemanticHighlightClassification.XmlDocCommentComment,
                [ClassificationTypeNames.XmlDocCommentDelimiter] = SemanticHighlightClassification.XmlDocCommentDelimiter,
                [ClassificationTypeNames.XmlDocCommentEntityReference] = SemanticHighlightClassification.XmlDocCommentEntityReference,
                [ClassificationTypeNames.XmlDocCommentName] = SemanticHighlightClassification.XmlDocCommentName,
                [ClassificationTypeNames.XmlDocCommentProcessingInstruction] = SemanticHighlightClassification.XmlDocCommentProcessingInstruction,
                [ClassificationTypeNames.XmlDocCommentText] = SemanticHighlightClassification.XmlDocCommentText,
                [ClassificationTypeNames.XmlLiteralAttributeName] = SemanticHighlightClassification.XmlLiteralAttributeName,
                [ClassificationTypeNames.XmlLiteralAttributeQuotes] = SemanticHighlightClassification.XmlLiteralAttributeQuotes,
                [ClassificationTypeNames.XmlLiteralAttributeValue] = SemanticHighlightClassification.XmlLiteralAttributeValue,
                [ClassificationTypeNames.XmlLiteralCDataSection] = SemanticHighlightClassification.XmlLiteralCDataSection,
                [ClassificationTypeNames.XmlLiteralComment] = SemanticHighlightClassification.XmlLiteralComment,
                [ClassificationTypeNames.XmlLiteralDelimiter] = SemanticHighlightClassification.XmlLiteralDelimiter,
                [ClassificationTypeNames.XmlLiteralEmbeddedExpression] = SemanticHighlightClassification.XmlLiteralEmbeddedExpression,
                [ClassificationTypeNames.XmlLiteralEntityReference] = SemanticHighlightClassification.XmlLiteralEntityReference,
                [ClassificationTypeNames.XmlLiteralName] = SemanticHighlightClassification.XmlLiteralName,
                [ClassificationTypeNames.XmlLiteralProcessingInstruction] = SemanticHighlightClassification.XmlLiteralProcessingInstruction,
                [ClassificationTypeNames.XmlLiteralText] = SemanticHighlightClassification.XmlLiteralText,
                [ClassificationTypeNames.RegexComment] = SemanticHighlightClassification.RegexComment,
                [ClassificationTypeNames.RegexCharacterClass] = SemanticHighlightClassification.RegexCharacterClass,
                [ClassificationTypeNames.RegexAnchor] = SemanticHighlightClassification.RegexAnchor,
                [ClassificationTypeNames.RegexQuantifier] = SemanticHighlightClassification.RegexQuantifier,
                [ClassificationTypeNames.RegexGrouping] = SemanticHighlightClassification.RegexGrouping,
                [ClassificationTypeNames.RegexAlternation] = SemanticHighlightClassification.RegexAlternation,
                [ClassificationTypeNames.RegexText] = SemanticHighlightClassification.RegexText,
                [ClassificationTypeNames.RegexSelfEscapedCharacter] = SemanticHighlightClassification.RegexSelfEscapedCharacter,
                [ClassificationTypeNames.RegexOtherEscape] = SemanticHighlightClassification.RegexOtherEscape,
            };

        private static readonly Dictionary<string, SemanticHighlightModifier> _modifierMap =
            new Dictionary<string, SemanticHighlightModifier>
            {
                [ClassificationTypeNames.StaticSymbol] = SemanticHighlightModifier.Static,
            };

        private readonly OmniSharpWorkspace _workspace;
    }
}
