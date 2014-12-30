using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNet.Mvc;
using Microsoft.CodeAnalysis;
using Xunit;
using OmniSharp.Models;

namespace OmniSharp.Tests
{
    public class TestContextFacts
    {
        [Fact]
        public async void CanGetMethodNameWithCursorOnName()
        {
            var source = @"public class TestClass
                            {
                                [Fact]
                                public void ThisIs$ATest()
                                {
                                }
                            }";

            var testInfo = await GetTestInfo(source);

            Assert.Equal("ThisIsATest", testInfo.MethodName);
        }

        [Fact]
        public async void CanGetMethodNameWithCursorOnMethodBody()
        {
            var source = @"
                    public class TestClass
                    {
                        [Fact]
                        public void ThisIsATest()
                        {
                            int t$en = 10;
                        }
                    }";

            var testInfo = await GetTestInfo(source);

            Assert.Equal("ThisIsATest", testInfo.MethodName);
        }

        [Fact]
        public async void CanGetMethodNameWithCursorOnMethodAccessModifier()
        {
            var source = @"
                    public class TestClass
                    {
                        [Test]
                        publ$ic void ThisIsATest()
                        {
                            int ten = 10;
                        }
                    }";

            var testInfo = await GetTestInfo(source);

            Assert.Equal("ThisIsATest", testInfo.MethodName);
        }

        [Fact]
        public async void CanGetMethodNameWithCursorOnMethodTestAttribute()
        {
            var source = @"
                    public class TestClass
                    {
                        public void ThisIsNotATest()
                        {

                        }

                        [$Test]
                        public void ThisIsATest()
                        {
                        }
                    }";

            var testInfo = await GetTestInfo(source);

            Assert.Equal("ThisIsATest", testInfo.MethodName);
        }

        [Fact]
        public async void CanGetTypeName()
        {
            var source = @"
                    public class TestClass
                    {
                        [$Test]
                        public void ThisIsATest()
                        {
                        }
                    }";
            
            var testInfo = await GetTestInfo(source);

            Assert.Equal("TestClass", testInfo.TypeName);
        }

        [Fact]
        public async void CanGetFullyQualifiedTypeName()
        {
            var source = @"
                    public namespace Namespace.Something
                    {
                        public class TestClass
                        {
                            [$Test]
                            public void ThisIsATest()
                            {
                            }
                        }
                    }";
            
            var testInfo = await GetTestInfo(source);

            Assert.Equal("Namespace.Something.TestClass", testInfo.TypeName);
        }

        private async Task<GetContextResponse> GetTestInfo(string source)
        {
            var workspace = TestHelpers.CreateSimpleWorkspace(source);
            var controller = new OmnisharpController(workspace);
            var request = CreateRequest(source);
            var actionResult = await controller.GetContext(request);
            return (actionResult as ObjectResult).Value as GetContextResponse;
        }

        private TestCommandRequest CreateRequest(string source, string fileName = "dummy.cs")
        {
            var lineColumn = TestHelpers.GetLineAndColumnFromDollar(source);
            return new TestCommandRequest
            {
                Line = lineColumn.Line,
                Column = lineColumn.Column,
                FileName = fileName,
                Buffer = source.Replace("$", ""),
                Type = TestCommandRequest.TestCommandType.Single
            };
        }
    }
}