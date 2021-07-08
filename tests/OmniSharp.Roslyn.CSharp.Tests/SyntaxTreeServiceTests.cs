using Microsoft.CodeAnalysis.Text;
using OmniSharp.Extensions;
using OmniSharp.Models.v1.SyntaxTree;
using OmniSharp.Models.V2;
using OmniSharp.Roslyn.CSharp.Services.SyntaxTree;
using System;
using System.Linq;
using System.Threading.Tasks;
using TestUtility;
using Xunit;
using Xunit.Abstractions;

#nullable enable

namespace OmniSharp.Roslyn.CSharp.Tests
{
    public class SyntaxTreeServiceTests : IDisposable
    {
        private const string TestFile =
@"{|compilationUnit:{|usingDirective:using{|usingWhitespace: |}System;
|}{|namespaceDeclaration:
namespace N
{
    {|interfaceDeclaration:interface I {}|}
    class C
    {
        public int Prop { get; set; }
        public object Field;
{|mDeclaration:{|publicMTokenRange:        p{|publicMTokenPoint:|}ublic |}void M<T>(C c)
        {
            // Comment
            T t = {|defaultUsage:default|};
            {|arrayType:T[]|} arr = new T[0];

            {|goodMCall:M<int>(null)|};
            {|badMCall:M()|};
        }
|}
    }
}|}{|eof:|}|}";
        private const string TestFileName = "test.cs";

        private readonly TestContent _testContent;
        private SourceText? _sourceText;

        private readonly OmniSharpTestHost _testHost;

        public SyntaxTreeServiceTests(ITestOutputHelper output)
        {
            // Note: not using a shared context because this is a stateful API. We don't want state from previous runs
            // hanging around.
            _testContent = TestContent.Parse(TestFile);
            _testHost = OmniSharpTestHost.Create(testOutput: output);
            _testHost.AddFilesToWorkspace(new TestFile(TestFileName, _testContent));
        }

        [Fact]
        public async Task SyntaxRequestWithEmptyParent_ReturnsCompilationUnit()
        {
            var result = await SyntaxTreeRequest(node: null);
            Assert.Single(result.TreeItems);
            Assert.Equal(await Node("CompilationUnit", hasChildren: true, id: 0, range: "compilationUnit"), result.TreeItems.Single());
        }

        [Fact]
        public async Task SyntaxRequestWithCompilationUnitParent_ReturnsChildren()
        {
            var compilationUnit = (await SyntaxTreeRequest(node: null)).TreeItems.Single();

            var result = await SyntaxTreeRequest(compilationUnit);

            Assert.Equal(
                new[]
                {
                    await Node("UsingDirective", hasChildren: true, id: 1, range: "usingDirective"),
                    await Node("NamespaceDeclaration", hasChildren: true, id: 8, range: "namespaceDeclaration"),
                    await Token("EndOfFileToken", hasChildren: false, id: 182, range: "eof")
                }
                , result.TreeItems);
        }

        [Fact]
        public async Task SyntaxRequestWithToken_ReturnsUnderlyingTrivia()
        {
            var compilationUnit = (await SyntaxTreeRequest(node: null)).TreeItems.Single();
            var usingDeclaration = (await SyntaxTreeRequest(node: compilationUnit)).TreeItems.First();
            var usingToken = (await SyntaxTreeRequest(node: usingDeclaration)).TreeItems.First();
            var trivia = await SyntaxTreeRequest(node: usingToken);

            Assert.Equal(new[]
            {
                await Trivia(isLeading: false, "WhitespaceTrivia", id: 3, range: "usingWhitespace")
            }, trivia.TreeItems);
        }

        [Fact]
        public async Task SyntaxRequestForNodeWithoutSetup_ReturnsNothing()
        {
            var result = await SyntaxTreeRequest(await Node("CompilationUnit", hasChildren: true, id: 0, "compilationUnit"));
            Assert.Empty(result.TreeItems);
        }

        [Fact]
        public async Task GetParent_ReturnsSameNode()
        {
            var compilationUnit = (await SyntaxTreeRequest(node: null)).TreeItems.Single();
            var usingDeclaration = (await SyntaxTreeRequest(node: compilationUnit)).TreeItems.First();

            var response = await SyntaxParentRequest(usingDeclaration);
            Assert.Equal(compilationUnit, response.Parent);
        }

        [Fact]
        public async Task GetParentWithoutSetup_ReturnsNothing()
        {
            var result = await SyntaxParentRequest(await Node("CompilationUnit", hasChildren: true, id: 0, "compilationUnit"));
            Assert.Null(result.Parent);
        }

        [Fact]
        public async Task GetAtRangeWithMethodSyntax_ReturnsMethodDeclaration()
        {
            var result = await SyntaxNodeAtRange("mDeclaration");
            Assert.Equal(await Node("MethodDeclaration", hasChildren: true, id: 69, range: "mDeclaration"), result.Node);
        }

        [Fact]
        public async Task GetAtRangeWithPublicToken_ReturnsJustToken()
        {
            var result = await SyntaxNodeAtRange("publicMTokenPoint");
            Assert.Equal(await Token("PublicKeyword", hasChildren: true, id: 70, "publicMTokenRange"), result.Node);
        }

        [Fact]
        public async Task NodeInfoOnCompilationUnit_ReturnsInfo()
        {
            var compilationUnit = (await SyntaxTreeRequest(node: null)).TreeItems.Single();
            var result = await SyntaxNodeInfoRequest(compilationUnit);

            AssertNodeInfoEqual(
                NodeInfo(
                    Class("Microsoft.CodeAnalysis.CSharp.Syntax.CompilationUnitSyntax"),
                    nodeSyntaxKind: "CompilationUnit",
                    semanticClassification: "keyword"),
                result);
        }

        [Fact]
        public async Task NodeInfoOnNamespaceDeclaration_ReturnsInfo()
        {
            var namespaceNode = (await SyntaxNodeAtRange("namespaceDeclaration")).Node;
            var result = await SyntaxNodeInfoRequest(namespaceNode!);
            AssertNodeInfoEqual(
                NodeInfo(
                    Class("Microsoft.CodeAnalysis.CSharp.Syntax.NamespaceDeclarationSyntax"),
                    "NamespaceDeclaration",
                    semanticClassification: "keyword",
                    nodeDeclaredSymbol: new() { Symbol = "N", SymbolKind = "namespace" }),
                result);
        }

        [Fact]
        public async Task NodeInfoOnInterfaceDeclaration_ReturnsInfo()
        {
            var interfaceNode = (await SyntaxNodeAtRange("interfaceDeclaration")).Node;
            var result = await SyntaxNodeInfoRequest(interfaceNode!);
            AssertNodeInfoEqual(
                NodeInfo(
                    Class("Microsoft.CodeAnalysis.CSharp.Syntax.InterfaceDeclarationSyntax"),
                    "InterfaceDeclaration",
                    semanticClassification: "keyword",
                    nodeDeclaredSymbol: new() { Symbol = "I", SymbolKind = "interface" }),
                result);
        }

        [Fact]
        public async Task NodeInfoOnExpression_ReturnsInfo()
        {
            var defaultUsage = (await SyntaxNodeAtRange("defaultUsage")).Node;
            var result = await SyntaxNodeInfoRequest(defaultUsage!);
            AssertNodeInfoEqual(
                NodeInfo(
                    Class("Microsoft.CodeAnalysis.CSharp.Syntax.LiteralExpressionSyntax"),
                    "DefaultLiteralExpression",
                    semanticClassification: "keyword",
                    typeInfo: new()
                    {
                        Type = new() { Symbol = "T", SymbolKind = "typeparameter" },
                        ConvertedType = new() { Symbol = "T", SymbolKind = "typeparameter" },
                        Conversion = "DefaultLiteral"
                    }),
                result);
        }

        [Fact]
        public async Task NodeInfoOnArrayType_ReturnsInfo()
        {
            var arrayType = (await SyntaxNodeAtRange("arrayType")).Node;
            var result = await SyntaxNodeInfoRequest(arrayType!);
            AssertNodeInfoEqual(
                NodeInfo(
                    Class("Microsoft.CodeAnalysis.CSharp.Syntax.ArrayTypeSyntax"),
                    "ArrayType",
                    semanticClassification: "type parameter name",
                    symbolInfo: new()
                    {
                        Symbol = new() { Symbol = "T[]", SymbolKind = "array" },
                        CandidateReason = "None",
                        CandidateSymbols = new SymbolAndKind[0]
                    },
                    typeInfo: new()
                    {
                        Type = new() { Symbol = "T[]", SymbolKind = "array" },
                        ConvertedType = new() { Symbol = "T[]", SymbolKind = "array" },
                        Conversion = "Identity"
                    }),
                result);
        }

        [Fact]
        public async Task NodeInfoOnGoodCall_ReturnsInfo()
        {
            var goodMCall = (await SyntaxNodeAtRange("goodMCall")).Node;
            var result = await SyntaxNodeInfoRequest(goodMCall!);
            AssertNodeInfoEqual(
                NodeInfo(
                    Class("Microsoft.CodeAnalysis.CSharp.Syntax.InvocationExpressionSyntax"),
                    "InvocationExpression",
                    semanticClassification: "method name",
                    symbolInfo: new()
                    {
                        Symbol = new() { Symbol = "void C.M<int>(C c)", SymbolKind = "method" },
                        CandidateReason = "None",
                        CandidateSymbols = new SymbolAndKind[0]
                    },
                    typeInfo: new()
                    {
                        Type = new() { Symbol = "void", SymbolKind = "struct" },
                        ConvertedType = new() { Symbol = "void", SymbolKind = "struct" },
                        Conversion = "Identity"
                    }),
                result);
        }

        [Fact]
        public async Task NodeInfoOnBadCall_ReturnsInfo()
        {
            var badMCall = (await SyntaxNodeAtRange("badMCall")).Node;
            var result = await SyntaxNodeInfoRequest(badMCall!);
            AssertNodeInfoEqual(
                NodeInfo(
                    Class("Microsoft.CodeAnalysis.CSharp.Syntax.InvocationExpressionSyntax"),
                    "InvocationExpression",
                    semanticClassification: "identifier",
                    symbolInfo: new()
                    {
                        Symbol = SymbolAndKind.Null,
                        CandidateReason = "OverloadResolutionFailure",
                        CandidateSymbols = new SymbolAndKind[] { new() { Symbol = "void C.M<T>(C c)", SymbolKind = "method" } }
                    },
                    typeInfo: new()
                    {
                        Type = new() { Symbol = "void", SymbolKind = "struct" },
                        ConvertedType = new() { Symbol = "void", SymbolKind = "struct" },
                        Conversion = "Identity"
                    }),
                result);
        }

        [Fact]
        public async Task NodeInfoOnToken_ReturnsInfo()
        {
            var publicKeyword = (await SyntaxNodeAtRange("publicMTokenPoint")).Node;
            var result = await SyntaxNodeInfoRequest(publicKeyword!);
            AssertNodeInfoEqual(
                NodeInfo(
                    Struct("Microsoft.CodeAnalysis.SyntaxToken"),
                    "PublicKeyword",
                    semanticClassification: "keyword"),
                result);
        }

        [Fact]
        public async Task NodeInfoOnTrivia_ReturnsInfo()
        {
            var compilationUnit = (await SyntaxTreeRequest(node: null)).TreeItems.Single();
            var usingDeclaration = (await SyntaxTreeRequest(node: compilationUnit)).TreeItems.First();
            var usingToken = (await SyntaxTreeRequest(node: usingDeclaration)).TreeItems.First();
            var trivia = (await SyntaxTreeRequest(node: usingToken)).TreeItems.Single();
            var result = await SyntaxNodeInfoRequest(trivia);
            AssertNodeInfoEqual(
                NodeInfo(
                    Struct("Microsoft.CodeAnalysis.SyntaxTrivia"),
                    "Trailing: WhitespaceTrivia"),
                result);
        }

        private static SymbolAndKind Class(string symbol) => new() { Symbol = symbol, SymbolKind = "class" };

        private static SymbolAndKind Struct(string symbol) => new() { Symbol = symbol, SymbolKind = "struct" };

        private async Task<SyntaxTreeNode> Node(string symbol, bool hasChildren, int id, string range)
            => new()
            {
                NodeType = Class(symbol),
                HasChildren = hasChildren,
                Id = id,
                Range = await GetRange(range)
            };

        private async Task<SyntaxTreeNode> Token(string symbol, bool hasChildren, int id, string range)
            => new()
            {
                NodeType = Struct(symbol),
                HasChildren = hasChildren,
                Id = id,
                Range = await GetRange(range)
            };

        private async Task<SyntaxTreeNode> Trivia(bool isLeading, string symbol, int id, string range)
            => new()
            {
                NodeType = Struct($"{(isLeading ? "Leading:" : "Trailing:")} {symbol}"),
                HasChildren = false,
                Id = id,
                Range = await GetRange(range)
            };

        private SyntaxNodeInfoResponse NodeInfo(
            SymbolAndKind nodeType,
            string nodeSyntaxKind,
            string? semanticClassification = null,
            NodeSymbolInfo? symbolInfo = null,
            NodeTypeInfo? typeInfo = null,
            SymbolAndKind? nodeDeclaredSymbol = null)
            => new SyntaxNodeInfoResponse
            {
                NodeType = nodeType,
                NodeSyntaxKind = nodeSyntaxKind,
                SemanticClassification = semanticClassification,
                NodeSymbolInfo = symbolInfo,
                NodeTypeInfo = typeInfo,
                NodeDeclaredSymbol = nodeDeclaredSymbol ?? SymbolAndKind.Null
            };

        private void AssertNodeInfoEqual(SyntaxNodeInfoResponse expected, SyntaxNodeInfoResponse? actual)
        {
            Assert.NotNull(actual);
            Assert.Equal(expected.NodeType, actual!.NodeType);
            Assert.Equal(expected.NodeSyntaxKind, actual.NodeSyntaxKind);
            Assert.Equal(expected.SemanticClassification, actual.SemanticClassification);
            Assert.Equal(expected.NodeSymbolInfo, actual.NodeSymbolInfo);
            Assert.Equal(expected.NodeTypeInfo, actual.NodeTypeInfo);
            Assert.Equal(expected.NodeDeclaredSymbol, actual.NodeDeclaredSymbol);
            // Don't take a dependency on the number of properties in nodes, as that's an implementation detail of the nodes.
            // Just assert that some are present.
            Assert.True(actual.Properties.Count > 0);
        }

        private async Task<SyntaxTreeResponse> SyntaxTreeRequest(SyntaxTreeNode? node)
        {
            var syntaxRequestHandler = _testHost.GetRequestHandler<SyntaxTreeService>(OmniSharpEndpoints.SyntaxTree);

            return await syntaxRequestHandler.Handle(new SyntaxTreeRequest { FileName = TestFileName, Parent = node });
        }

        private async Task<SyntaxNodeParentResponse> SyntaxParentRequest(SyntaxTreeNode node)
        {
            var syntaxParentHandler = _testHost.GetRequestHandler<SyntaxTreeService>(OmniSharpEndpoints.SyntaxTreeParentNode);
            return await syntaxParentHandler.Handle(new SyntaxNodeParentRequest { Child = node });
        }

        private async Task<SyntaxNodeAtRangeResponse> SyntaxNodeAtRange(string range)
        {
            var syntaxNodeAtRangeHandler = _testHost.GetRequestHandler<SyntaxTreeService>(OmniSharpEndpoints.SyntaxNodeAtRange);
            return await syntaxNodeAtRangeHandler.Handle(new SyntaxNodeAtRangeRequest { FileName = TestFileName, Range = await GetRange(range) });
        }

        private async Task<SyntaxNodeInfoResponse?> SyntaxNodeInfoRequest(SyntaxTreeNode node)
        {
            var syntaxNodeInfoHandler = _testHost.GetRequestHandler<SyntaxTreeService>(OmniSharpEndpoints.SyntaxTreeNodeInfo);
            return await syntaxNodeInfoHandler.Handle(new SyntaxNodeInfoRequest { Node = node });
        }

        private async Task<Range> GetRange(string name)
        {
            var span = _testContent.GetSpan(name);
            _sourceText ??= await _testHost.Workspace.GetDocument(TestFileName).GetTextAsync();
            return _sourceText.GetRangeFromSpan(span);
        }

        public void Dispose()
        {
            _testHost.Dispose();
        }
    }
}
