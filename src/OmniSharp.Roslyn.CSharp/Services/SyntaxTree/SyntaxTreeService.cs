using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Classification;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Text;
using Microsoft.Extensions.Logging;
using OmniSharp.Extensions;
using OmniSharp.Mef;
using OmniSharp.Models.v1.SyntaxTree;
using OmniSharp.Models.V2;
using System;
using System.Collections.Generic;
using System.Composition;
using System.Diagnostics;
using System.Linq;
using System.Threading.Tasks;

#nullable enable

namespace OmniSharp.Roslyn.CSharp.Services.SyntaxTree
{
    [OmniSharpHandler(OmniSharpEndpoints.SyntaxTree, LanguageNames.CSharp)]
    [OmniSharpHandler(OmniSharpEndpoints.SyntaxNodeAtRange, LanguageNames.CSharp)]
    [OmniSharpHandler(OmniSharpEndpoints.SyntaxTreeParentNode, LanguageNames.CSharp)]
    [OmniSharpHandler(OmniSharpEndpoints.SyntaxTreeNodeInfo, LanguageNames.CSharp)]
    [Shared]
    public class SyntaxTreeService :
        IRequestHandler<SyntaxTreeRequest, SyntaxTreeResponse>,
        IRequestHandler<SyntaxNodeAtRangeRequest, SyntaxNodeAtRangeResponse>,
        IRequestHandler<SyntaxNodeParentRequest, SyntaxNodeParentResponse>,
        IRequestHandler<SyntaxNodeInfoRequest, SyntaxNodeInfoResponse?>
    {
        private readonly OmniSharpWorkspace _workspace;
        private readonly Dictionary<int, SyntaxNodeOrTokenOrTrivia> _nodeMap = new();
        private readonly Dictionary<SyntaxNodeOrTokenOrTrivia, int> _idMap = new();
        private string? _mapFile;
        private VersionStamp _syntaxVersion = VersionStamp.Default;
        private readonly object _lock = new();
        private readonly ILogger<SyntaxTreeService> _logger;

        [ImportingConstructor]
        public SyntaxTreeService(OmniSharpWorkspace workspace, ILoggerFactory loggerFactory)
        {
            _workspace = workspace;
            _logger = loggerFactory.CreateLogger<SyntaxTreeService>();
        }

        public async Task<SyntaxTreeResponse> Handle(SyntaxTreeRequest request)
        {
            if (request.Parent != null && _mapFile != request.FileName)
            {
                _logger.LogError($"Recieved request for parent of node from a different file.\nRequested File: {request.FileName}\nLast mapped file: {_mapFile}");
                return new SyntaxTreeResponse { TreeItems = Array.Empty<SyntaxTreeNode>() };
            }

            var document = _workspace.GetDocument(request.FileName);

            if (document is null)
            {
                _logger.LogTrace($"Document {request.FileName} was not found");
                return new SyntaxTreeResponse { TreeItems = Array.Empty<SyntaxTreeNode>() };
            }

            var syntaxNode = await document.GetSyntaxRootAsync();
            if (syntaxNode is null)
            {
                _logger.LogWarning($"Document {request.FileName} does not have a syntax tree");
                return new SyntaxTreeResponse { TreeItems = Array.Empty<SyntaxTreeNode>() };
            }

            var text = await document.GetTextAsync();
            var version = await document.GetSyntaxVersionAsync();
            Debug.Assert(text is not null);

            lock (_lock)
            {
                if (request.Parent is null)
                {
                    BuildIdMap(request.FileName, syntaxNode, version);
                    return new SyntaxTreeResponse { TreeItems = new[] { NodeOrTokenOrTriviaToTreeItem(syntaxNode, text!, 0) } };
                }
                else
                {
                    var parentItem = request.Parent;
                    if (!_nodeMap.ContainsKey(request.Parent.Id))
                    {
                        var range = parentItem.Range;
                        _logger.LogError($"{parentItem.NodeType} [{range.Start.Line}:{range.Start.Column}:{range.End.Line}:{range.End}) in file {request.FileName} does not have an ID");
                        return new SyntaxTreeResponse { TreeItems = Array.Empty<SyntaxTreeNode>() };
                    }

                    var parent = _nodeMap[parentItem.Id];

                    return new SyntaxTreeResponse { TreeItems = parent.GetChildren().Select(s => NodeOrTokenOrTriviaToTreeItem(s, text!, _idMap[s])) };
                }
            }
        }

        public async Task<SyntaxNodeAtRangeResponse> Handle(SyntaxNodeAtRangeRequest request)
        {
            var document = _workspace.GetDocument(request.FileName);

            if (document is null)
            {
                _logger.LogTrace($"Document {request.FileName} was not found");
                return new SyntaxNodeAtRangeResponse { Node = null };
            }

            var syntaxNode = await document.GetSyntaxRootAsync();
            if (syntaxNode is null)
            {
                _logger.LogWarning($"Document {request.FileName} does not have a syntax tree");
                return new SyntaxNodeAtRangeResponse { Node = null };
            }

            var text = await document.GetTextAsync();
            var version = await document.GetSyntaxVersionAsync();
            Debug.Assert(text is not null);

            lock (_lock)
            {
                if (_mapFile != request.FileName && version != _syntaxVersion)
                {
                    BuildIdMap(request.FileName, syntaxNode, version);
                }

                // Find the nearest token or node to the given position. Don't include trivia, it's likely not what
                // the user wanted.
                var span = text.GetSpanFromRange(request.Range);
                SyntaxNodeOrToken element = span.Length == 0 ? syntaxNode.FindToken(span.Start) : syntaxNode.FindNode(span);

                return new SyntaxNodeAtRangeResponse { Node = NodeOrTokenOrTriviaToTreeItem(element, text!, _idMap[element]) };
            }
        }

        public async Task<SyntaxNodeParentResponse> Handle(SyntaxNodeParentRequest request)
        {
            if (_mapFile is null)
            {
                _logger.LogWarning("Requested parents for node with no active file");
                return new SyntaxNodeParentResponse { Parent = null };
            }

            var document = _workspace.GetDocument(_mapFile);

            if (document is null)
            {
                _logger.LogTrace($"Document {_mapFile} was not found");
                return new SyntaxNodeParentResponse { Parent = null };
            }

            var text = await document.GetTextAsync();
            Debug.Assert(text is not null);

            var version = await document.GetSyntaxVersionAsync();

            lock (_lock)
            {
                if (_mapFile != document.FilePath || _syntaxVersion != version)
                {
                    _logger.LogInformation("New request queued while fetching parent nodes, cancelling.");
                    return new SyntaxNodeParentResponse { Parent = null };
                }

                var child = _nodeMap[request.Child.Id];

                if (child.Node is CompilationUnitSyntax)
                {
                    return new SyntaxNodeParentResponse { Parent = null };
                }

                return new SyntaxNodeParentResponse { Parent = NodeOrTokenOrTriviaToTreeItem(child.Parent, text!, _idMap[child.Parent]) };
            }
        }

        public async Task<SyntaxNodeInfoResponse?> Handle(SyntaxNodeInfoRequest request)
        {
            if (_mapFile is null)
            {
                _logger.LogWarning("Requested info for node with no active file");
                return null;
            }

            var document = _workspace.GetDocument(_mapFile);

            if (document is null)
            {
                _logger.LogTrace($"Document {_mapFile} was not found");
                return null;
            }

            var version = await document.GetSyntaxVersionAsync();

            SyntaxNodeOrTokenOrTrivia item;

            lock (_lock)
            {
                if (_mapFile != document.FilePath || _syntaxVersion != version)
                {
                    _logger.LogInformation("New request queued while fetching parent nodes, cancelling.");
                    return null;
                }

                item = _nodeMap[request.Node.Id];
            }

            var itemSpan = item.GetSpan();
            var classification = await Classifier.GetClassifiedSpansAsync(document, itemSpan);

            var response = new SyntaxNodeInfoResponse
            {
                NodeType = new() { Symbol = item.GetUnderlyingType().ToString(), SymbolKind = item.Node is null ? SymbolKinds.Struct : SymbolKinds.Class },
                NodeSyntaxKind = item.Kind(),
                Properties = item.GetPublicProperties(),
                SemanticClassification = classification.FirstOrDefault().ClassificationType,
            };

            await populateNodeInformation(item.Node);

            return response;

            async Task populateNodeInformation(SyntaxNode? node)
            {
                if (node is null) return;

                var model = await document.GetSemanticModelAsync();
                if (model is null) return;

                var typeInfo = model.GetTypeInfo(node);
                if (typeInfo.Type is not null || typeInfo.ConvertedType is not null)
                {
                    response.NodeTypeInfo = new NodeTypeInfo
                    {
                        Type = getSymbolAndKind(typeInfo.Type, model, node.SpanStart),
                        ConvertedType = getSymbolAndKind(typeInfo.ConvertedType, model, node.SpanStart),
                        Conversion = model.GetConversion(node).ToString()
                    };
                }

                var symbolInfo = model.GetSymbolInfo(node);
                if (symbolInfo.Symbol is not null || !symbolInfo.CandidateSymbols.IsDefaultOrEmpty)
                {
                    response.NodeSymbolInfo = new NodeSymbolInfo
                    {
                        Symbol = getSymbolAndKind(symbolInfo.Symbol, model, node.SpanStart),
                        CandidateSymbols = symbolInfo.CandidateSymbols.Select(s => getSymbolAndKind(s, model, node.SpanStart)),
                        CandidateReason = symbolInfo.CandidateReason.ToString()
                    };
                }

                response.NodeDeclaredSymbol = getSymbolAndKind(model.GetDeclaredSymbol(node), model, node.SpanStart);
            }

            static SymbolAndKind getSymbolAndKind(ISymbol? symbol, SemanticModel model, int position)
            {
                return symbol is null
                    ? SymbolAndKind.Null
                    : new() { Symbol = symbol.ToMinimalDisplayString(model, position), SymbolKind = symbol.GetKindString() };
            }
        }

        private void BuildIdMap(string fileName, SyntaxNode syntaxNode, VersionStamp version)
        {
            // First time we've seen this file. Build the map
            int id = 0;
            _nodeMap.Clear();
            _idMap.Clear();
            foreach (var nodeOrToken in syntaxNode.DescendantNodesAndTokensAndSelf())
            {
                _nodeMap[id] = nodeOrToken;
                _idMap[nodeOrToken] = id;
                id++;

                if (nodeOrToken.IsToken)
                {
                    var token = nodeOrToken.AsToken();
                    if (token.HasLeadingTrivia)
                    {
                        MapTrivia(ref id, token.LeadingTrivia, isLeading: true);
                    }

                    if (token.HasTrailingTrivia)
                    {
                        MapTrivia(ref id, token.TrailingTrivia, isLeading: false);
                    }
                }
            }

            _mapFile = fileName;
            _syntaxVersion = version;
        }

        private void MapTrivia(ref int id, SyntaxTriviaList triviaList, bool isLeading)
        {
            foreach (var element in triviaList)
            {
                var wrappedElement = new SyntaxNodeOrTokenOrTrivia(element, isLeading);
                _nodeMap[id] = wrappedElement;
                _idMap[wrappedElement] = id;
                id++;
            }
        }

        private static SyntaxTreeNode NodeOrTokenOrTriviaToTreeItem(SyntaxNodeOrTokenOrTrivia element, SourceText text, int id)
        {
            return new SyntaxTreeNode
            {
                NodeType = new() { Symbol = element.Kind(), SymbolKind = element.Node is null ? SymbolKinds.Struct : SymbolKinds.Class },
                HasChildren = element.HasChildren(),
                Id = id,
                Range = text.GetRangeFromSpan(element.GetFullSpan()),
            };
        }

        private struct SyntaxNodeOrTokenOrTrivia
        {
            private readonly SyntaxNodeOrToken? _nodeOrToken;
            private readonly SyntaxTrivia? _trivia;
            private readonly bool _isLeadingTrivia;
            public SyntaxNode? Node => _nodeOrToken?.AsNode();
            public SyntaxToken? Token => _nodeOrToken?.AsToken();
            public SyntaxTrivia? Trivia => _trivia;

            public static implicit operator SyntaxNodeOrTokenOrTrivia(SyntaxNode node) => new SyntaxNodeOrTokenOrTrivia(node);
            public static implicit operator SyntaxNodeOrTokenOrTrivia(SyntaxToken node) => new SyntaxNodeOrTokenOrTrivia(node);
            public static implicit operator SyntaxNodeOrTokenOrTrivia(SyntaxNodeOrToken nodeOrToken) => new SyntaxNodeOrTokenOrTrivia(nodeOrToken);

            public static bool operator ==(SyntaxNodeOrTokenOrTrivia left, SyntaxNodeOrTokenOrTrivia right)
            {
                return left.Equals(right);
            }

            public static bool operator !=(SyntaxNodeOrTokenOrTrivia left, SyntaxNodeOrTokenOrTrivia right)
            {
                return !(left == right);
            }

            public SyntaxNodeOrTokenOrTrivia(SyntaxNodeOrToken nodeOrToken)
            {
                _nodeOrToken = nodeOrToken;
                _trivia = null;
                _isLeadingTrivia = false;
            }

            public SyntaxNodeOrTokenOrTrivia(SyntaxTrivia trivia, bool isLeadingTrivia)
            {
                _trivia = trivia;
                _nodeOrToken = null;
                _isLeadingTrivia = isLeadingTrivia;
            }

            public string Kind()
            {
                return _nodeOrToken is { } n
                    ? n.Kind().ToString()
                    : (_isLeadingTrivia ? "Leading: " : "Trailing: ") + _trivia!.Value.Kind().ToString();
            }

            public bool HasChildren()
            {
                if (_nodeOrToken?.AsNode() is { } node)
                {
                    return node.ChildNodesAndTokens().Any();
                }
                else if (_nodeOrToken?.AsToken() is { } token)
                {
                    return token.HasLeadingTrivia || token.HasTrailingTrivia;
                }

                return false;
            }

            public IEnumerable<SyntaxNodeOrTokenOrTrivia> GetChildren()
            {
                if (_nodeOrToken is not { } nodeOrToken)
                {
                    return Array.Empty<SyntaxNodeOrTokenOrTrivia>();
                }

                if (nodeOrToken.AsNode() is { } node)
                {
                    return node.ChildNodesAndTokens().Select(s => (SyntaxNodeOrTokenOrTrivia)s);
                }
                else
                {
                    var token = nodeOrToken.AsToken();
                    return token.LeadingTrivia
                        .Select(s => new SyntaxNodeOrTokenOrTrivia(s, isLeadingTrivia: true))
                        .Concat(token.TrailingTrivia.Select(s => new SyntaxNodeOrTokenOrTrivia(s, isLeadingTrivia: false)));
                }
            }

            public TextSpan GetSpan()
            {
                if (_nodeOrToken is { } nodeOrToken)
                {
                    return nodeOrToken.Span;
                }
                else
                {
                    return _trivia!.Value.Span;
                }
            }

            public TextSpan GetFullSpan()
            {
                if (_nodeOrToken is { } nodeOrToken)
                {
                    return nodeOrToken.FullSpan;
                }
                else
                {
                    return _trivia!.Value.FullSpan;
                }
            }

            public SyntaxNodeOrToken Parent => _nodeOrToken is { Parent: var parent } ? parent : _trivia!.Value.Token;

            public Dictionary<string, string> GetPublicProperties()
            {
                var @object = _nodeOrToken is { } nodeOrToken
                    ? (nodeOrToken.AsNode() is { } node ? node : nodeOrToken.AsToken())
                    : (object)_trivia!.Value;

                var type = @object.GetType();

                return type.GetProperties().Where(p => p.Name != "Kind" && p.CanRead).ToDictionary(
                    static s => s.Name,
                    s => s.GetValue(@object) switch
                    {
                        string str => $@"""{str}""",
                        var val => val?.ToString() ?? "<null>"
                    });
            }

            public Type GetUnderlyingType()
            {
                return _nodeOrToken is { } nodeOrToken
                    ? nodeOrToken.AsNode()?.GetType() ?? nodeOrToken.AsToken().GetType()
                    : _trivia!.Value.GetType();
            }

            public override string ToString()
            {
                string nodeString = _nodeOrToken?.ToString() ?? "<null>";
                string leadingString = _trivia != null ? "" : _isLeadingTrivia ? "Leading " : "Trailing ";
                string triviaString = _trivia?.ToString() ?? "<null>";
                return $"({nodeString}, {leadingString}{triviaString})";
            }

            public override bool Equals(object obj)
            {
                return obj is SyntaxNodeOrTokenOrTrivia other &&
                    other._isLeadingTrivia == _isLeadingTrivia &&
                    other._nodeOrToken == _nodeOrToken &&
                    other._trivia == _trivia;
            }

            public override int GetHashCode()
            {
                int hashCode = -829259152;
                hashCode = hashCode * -1521134295 + _nodeOrToken.GetHashCode();
                hashCode = hashCode * -1521134295 + _trivia.GetHashCode();
                hashCode = hashCode * -1521134295 + _isLeadingTrivia.GetHashCode();
                return hashCode;
            }
        }
    }
}
