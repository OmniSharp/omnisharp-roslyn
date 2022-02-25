#nullable enable

using System.Collections.Generic;
using System.Threading.Tasks;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Formatting;
using OmniSharp.Models.Format;
using OmniSharp.Models.UpdateBuffer;
using OmniSharp.Roslyn.CSharp.Services.Buffer;
using OmniSharp.Roslyn.CSharp.Services.Formatting;
using TestUtility;
using Xunit;
using Xunit.Abstractions;

namespace OmniSharp.Roslyn.CSharp.Tests
{
    public class FormatAfterKeystrokeTests : AbstractTestFixture
    {
        public FormatAfterKeystrokeTests(ITestOutputHelper output, SharedOmniSharpHostFixture sharedOmniSharpHostFixture)
            : base(output, sharedOmniSharpHostFixture)
        {
        }

        [Theory]
        [InlineData("file.cs")]
        [InlineData("file.csx")]
        public async Task NoComment_OnEnter(string fileName)
        {
            await VerifyNoChange(fileName, "\n",
@"class C
{
$$
}");

        }

        [Theory]
        [InlineData("file.cs")]
        [InlineData("file.csx")]
        public async Task NoComment_OnSingleForwardSlash(string fileName)
        {
            await VerifyNoChange(fileName, "/",
@"class C
{
    /$$
}
");
        }

        [Theory]
        [InlineData("file.cs")]
        [InlineData("file.csx")]
        public async Task NoComment_OnDoubleForwardSlash(string fileName)
        {
            await VerifyNoChange(fileName, "/",
@"class C
{
    //$$
}
");
        }

        [Theory]
        [InlineData("file.cs")]
        [InlineData("file.csx")]
        public async Task NoComment_OnTripleForwardSlash_NoMember(string fileName)
        {
            await VerifyNoChange(fileName, "/",
@"class C
{
    ///$$
}
");
        }

        [Theory]
        [InlineData("file.cs")]
        [InlineData("file.csx")]
        public async Task Comment_OnTripleForwardSlash_BeforeMethod(string fileName)
        {
            await VerifyChange(fileName, "/",
@"class C
{
    ///$$
    public string M<T>(string param1, int param2) { }
}",
@"class C
{
    /// <summary>
    /// 
    /// </summary>
    /// <typeparam name=""T""></typeparam>
    /// <param name=""param1""></param>
    /// <param name=""param2""></param>
    /// <returns></returns>
    public string M<T>(string param1, int param2) { }
}");
        }

        [Theory]
        [InlineData("file.cs")]
        [InlineData("file.csx")]
        public async Task Comment_OnTripleForwardSlash_BeforeType(string fileName)
        {
            await VerifyChange(fileName, "/",
@"///$$
class C
{
    public string M(string param1, int param2) { }
}",
@"/// <summary>
/// 
/// </summary>
class C
{
    public string M(string param1, int param2) { }
}");
        }

        [Theory]
        [InlineData("file.cs")]
        [InlineData("file.csx")]
        public async Task Comment_OnTripleForwardSlash_BeforeProperty(string fileName)
        {
            await VerifyChange(fileName, "/",
@"class C
{
    ///$$
    public int Prop { get; set; }
}",
@"class C
{
    /// <summary>
    /// 
    /// </summary>
    public int Prop { get; set; }
}");
        }

        [Theory]
        [InlineData("file.cs")]
        [InlineData("file.csx")]
        public async Task Comment_OnTripleForwardSlash_BeforeIndexer(string fileName)
        {
            await VerifyChange(fileName, "/",
@"class C
{
    ///$$
    public int this[int i] { get; set; }
}",
@"class C
{
    /// <summary>
    /// 
    /// </summary>
    /// <param name=""i""></param>
    /// <returns></returns>
    public int this[int i] { get; set; }
}");
        }

        [Theory]
        [InlineData("file.cs")]
        [InlineData("file.csx")]
        public async Task Comment_OnEnterInComment_BetweenTags_Newline(string fileName)
        {
            await VerifyChange(fileName, "\n",
@"class C
{
    /// <summary>
    ///
$$
    /// </summary>
    /// <param name=""param1""></param>
    /// <param name=""param2""></param>
    /// <returns></returns>
    public string M(string param1, int param2) { }
}",
@"class C
{
    /// <summary>
    ///
    /// 
    /// </summary>
    /// <param name=""param1""></param>
    /// <param name=""param2""></param>
    /// <returns></returns>
    public string M(string param1, int param2) { }
}");
        }

        [Theory]
        [InlineData("file.cs")]
        [InlineData("file.csx")]
        public async Task Comment_OnEnterInComment_BetweenTags_SameLine(string fileName)
        {
            await VerifyChange(fileName, "\n",
@"class C
{
    /// <summary>
    ///
    /// </summary>
    /// <param name=""param1"">
$$</param>
    /// <param name=""param2""></param>
    /// <returns></returns>
    public string M(string param1, int param2) { }
}",
@"class C
{
    /// <summary>
    ///
    /// </summary>
    /// <param name=""param1"">
    /// </param>
    /// <param name=""param2""></param>
    /// <returns></returns>
    public string M(string param1, int param2) { }
}");
        }

        [Theory]
        [InlineData("file.cs")]
        [InlineData("file.csx")]
        public async Task Comment_OnEnterInComment_AfterTags(string fileName)
        {
            await VerifyChange(fileName, "\n",
@"class C
{
    /// <summary>
    ///
    /// </summary>
    /// <param name=""param1""></param>
    /// <param name=""param2""></param>
    /// <returns></returns>
$$
    public string M(string param1, int param2) { }
}",
@"class C
{
    /// <summary>
    ///
    /// </summary>
    /// <param name=""param1""></param>
    /// <param name=""param2""></param>
    /// <returns></returns>
    /// 
    public string M(string param1, int param2) { }
}");
        }

        [Theory]
        [InlineData("file.cs")]
        [InlineData("file.csx")]
        public async Task Comment_OnTripleForwardSlash_VerbatimNames(string fileName)
        {
            await VerifyChange(fileName, "/",
@"class C
{
    ///$$
    public string M<@int>(string @float) { }
}",
@"class C
{
    /// <summary>
    /// 
    /// </summary>
    /// <typeparam name=""int""></typeparam>
    /// <param name=""float""></param>
    /// <returns></returns>
    public string M<@int>(string @float) { }
}");
        }

        [Theory]
        [InlineData("file.cs")]
        [InlineData("file.csx")]
        public async Task Comment_OnTripleForwardSlash_VoidMethod(string fileName)
        {
            await VerifyChange(fileName, "/",
@"class C
{
    ///$$
    public void M() { }
}",
@"class C
{
    /// <summary>
    /// 
    /// </summary>
    public void M() { }
}");
        }

        [Theory]
        [InlineData("file.cs")]
        [InlineData("file.csx")]
        public async Task NoComment_OnTripleForwardSlash_ExistingLineAbove(string fileName)
        {
            await VerifyNoChange(fileName, "/",
@"class C
{
    ///
    ///$$
    public void M() { }
}");
        }

        [Theory]
        [InlineData("file.cs")]
        [InlineData("file.csx")]
        public async Task NoComment_OnTripleForwardSlash_ExistingLineBelow_01(string fileName)
        {
            await VerifyNoChange(fileName, "/",
@"class C
{
    ///$$
    ///
    public void M() { }
}");
        }

        [Theory]
        [InlineData("file.cs")]
        [InlineData("file.csx")]
        public async Task NoComment_OnTripleForwardSlash_ExistingLineBelow_02(string fileName)
        {
            await VerifyNoChange(fileName, "/",
@"class C
{
    ///$$
    /// <summary></summary>
    public void M() { }
}");
        }

        [Theory]
        [InlineData("file.cs")]
        [InlineData("file.csx")]
        public async Task NoComment_OnTripleForwardSlash_InsideMethodBody(string fileName)
        {
            await VerifyNoChange(fileName, "/",
@"class C
{
    public void M()
    {
        ///$$
    }
}");
        }

        [Theory]
        [InlineData("file.cs")]
        [InlineData("file.csx")]
        public async Task NoComment_OnNewLine_InsideMethodBody(string fileName)
        {
            await VerifyNoChange(fileName, "/",
@"class C
{
    public void M()
    {
        ///
$$
    }
}");
        }

        protected override OmniSharpTestHost CreateSharedOmniSharpTestHost()
            => CreateOmniSharpHost(configurationData: new Dictionary<string, string>()
            {
                ["FormattingOptions:NewLine"] = System.Environment.NewLine
            });


        private async Task VerifyNoChange(string fileName, string typedCharacter, string originalMarkup)
        {
            var (response, _) = await GetResponse(originalMarkup, typedCharacter, fileName);
            Assert.Empty(response.Changes);
        }

        private async Task VerifyChange(string fileName, string typedCharacter, string originalMarkup, string expected)
        {
            var (response, testFile) = await GetResponse(originalMarkup, typedCharacter, fileName);
            Assert.NotNull(response);

            var fileChangedService = SharedOmniSharpTestHost.GetRequestHandler<UpdateBufferService>(OmniSharpEndpoints.UpdateBuffer);
            _ = await fileChangedService.Handle(new UpdateBufferRequest()
            {
                FileName = testFile.FileName,
                Changes = response.Changes,
                ApplyChangesTogether = true,
            });

            var actualDoc = SharedOmniSharpTestHost.Workspace.GetDocument(testFile.FileName);
            Assert.NotNull(actualDoc);
            var actualText = (await actualDoc.GetTextAsync()).ToString();
            AssertUtils.Equal(expected, actualText);
        }

        private async Task<(FormatRangeResponse, TestFile)> GetResponse(string text, string character, string fileName)
        {
            var file = new TestFile(fileName, text);
            SharedOmniSharpTestHost.AddFilesToWorkspace(file);
            var point = file.Content.GetPointFromPosition();

            var request = new FormatAfterKeystrokeRequest
            {
                Line = point.Line,
                Column = point.Offset,
                FileName = fileName,
                Character = character,
            };

            var requestHandler = SharedOmniSharpTestHost.GetRequestHandler<FormatAfterKeystrokeService>(OmniSharpEndpoints.FormatAfterKeystroke);
            return (await requestHandler.Handle(request), file);
        }
    }
}
