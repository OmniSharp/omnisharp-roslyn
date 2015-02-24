using System.Threading.Tasks;
using OmniSharp.AspNet5;
using OmniSharp.Filters;
using OmniSharp.Models;
using Xunit;

namespace OmniSharp.Tests
{
    public class TestCommandFacts
    {
        [Fact]
        public async Task CanGetMethodNameWithCursorOnName()
        {
            var source = @"public class TestClass
                            {
                                [Fact]
                                public void ThisIs$ATest()
                                {
                                }
                            }";

            var testInfo = await GetTestCommandAsync(source);

            Assert.Equal("k test --test TestClass.ThisIsATest", testInfo.TestCommand);
        }

        [Fact]
        public async Task CanGetMethodNameWithCursorOnMethodBody()
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

            var testInfo = await GetTestCommandAsync(source);

            Assert.Equal("k test --test TestClass.ThisIsATest", testInfo.TestCommand);
        }

        [Fact]
        public async Task CanGetMethodNameWithCursorOnMethodAccessModifier()
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

            var testInfo = await GetTestCommandAsync(source);

            Assert.Equal("k test --test TestClass.ThisIsATest", testInfo.TestCommand);
        }

        [Fact]
        public async Task CanGetMethodNameWithCursorOnMethodTestAttribute()
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

            var testInfo = await GetTestCommandAsync(source);
            Assert.Equal("k test --test TestClass.ThisIsATest", testInfo.TestCommand);
        }

        [Fact]
        public async Task CanGetFullyQualifiedTypeName()
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

            var testInfo = await GetTestCommandAsync(source);
            Assert.Equal("k test --test Namespace.Something.TestClass.ThisIsATest", testInfo.TestCommand);
        }

        [Fact]
        public async Task FallsBackToFixureWhenCursorIsNotOnMethod()
        {
            var source = @"
                        public class TestC$lass
                        {
                            [Test]
                            public void ThisIsATest()
                            {
                            }
                        }";

            var testInfo = await GetTestCommandAsync(source);
            Assert.Equal("k test --test TestClass", testInfo.TestCommand);
        }

        [Fact]
        public async Task FallsBackToFixureWhenCursorIsOnNamespace()
        {
            var source = @"
                        public namespace Some$Namespace
                        {
                            public class TestClass
                            {
                                [Test]
                                public void ThisIsATest()
                                {
                                }
                            }
                        }";

            var testInfo = await GetTestCommandAsync(source);
            Assert.Equal("k test --test SomeNamespace.TestClass", testInfo.TestCommand);
        }

        private async Task<GetTestCommandResponse> GetTestCommandAsync(string source)
        {
            var workspace = TestHelpers.CreateSimpleWorkspace(source);
            var context = new AspNet5Context();
            var projectName = "project.json";
            var projectCounter = 1;
            context.ProjectContextMapping.Add(projectName, projectCounter);
            context.Projects.Add(projectCounter, new Project
            {
                Path = "project.json",
                Commands = { { "test", "Xunit.KRunner" } }
            });

            var testCommandProviders = new[] { new AspNet5TestCommandProvider(context) };
            var controller = new TestCommandController(workspace, testCommandProviders);
            var request = CreateRequest(source);
            var bufferFilter = new UpdateBufferFilter(workspace);
            bufferFilter.OnActionExecuting(TestHelpers.CreateActionExecutingContext(request, controller));
            return await controller.GetTestCommand(request);
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
                Type = TestCommandType.Single
            };
        }
    }
}