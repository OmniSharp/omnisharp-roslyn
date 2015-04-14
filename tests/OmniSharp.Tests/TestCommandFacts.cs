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

            var testArgs = await GetTestCommandArgumentsAsync(source);

            Assert.Equal("test -method TestClass.ThisIsATest", testArgs);
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

            var testArgs = await GetTestCommandArgumentsAsync(source);

            Assert.Equal("test -method TestClass.ThisIsATest", testArgs);
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

            var testArgs = await GetTestCommandArgumentsAsync(source);

            Assert.Equal("test -method TestClass.ThisIsATest", testArgs);
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

            var testArgs = await GetTestCommandArgumentsAsync(source);
            Assert.Equal("test -method TestClass.ThisIsATest", testArgs);
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

            var testArgs = await GetTestCommandArgumentsAsync(source);
            Assert.Equal("test -method Namespace.Something.TestClass.ThisIsATest", testArgs);
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

            var testArgs = await GetTestCommandArgumentsAsync(source);
            Assert.Equal("test -class TestClass", testArgs);
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

            var testArgs = await GetTestCommandArgumentsAsync(source);
            Assert.Equal("test -class SomeNamespace.TestClass", testArgs);
        }

        [Fact]
        public async Task RunsTestFixture()
        {
            var source = @"
                        public namespace SomeNamespace
                        {
                            public class TestClass
                            {
                                [Test]
                                public void This$IsATest()
                                {
                                }
                            }
                        }";

            var testArgs = await GetTestCommandArgumentsAsync(source, TestCommandType.Fixture);
            Assert.Equal("test -class SomeNamespace.TestClass", testArgs);
        }

        [Fact]
        public async Task RunsTestFixtureWithCursorAboveFixture()
        {
            var source = @"
                        public namespace SomeNamespace
                        {$
                            public class TestClass
                            {
                                [Test]
                                public void ThisIsATest()
                                {
                                }
                            }
                        }";

            var testArgs = await GetTestCommandArgumentsAsync(source, TestCommandType.Fixture);
            Assert.Equal("test -class SomeNamespace.TestClass", testArgs);
        }

        private async Task<string> GetTestCommandArgumentsAsync(string source, TestCommandType testType = TestCommandType.Single)
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

            var testCommandProviders = new[] { new AspNet5TestCommandProvider(context, new FakeEnvironment(), new FakeLoggerFactory(), new FakeOmniSharpOptions()) };
            var controller = new TestCommandController(workspace, testCommandProviders);
            var lineColumn = TestHelpers.GetLineAndColumnFromDollar(source);

            var request = new TestCommandRequest
            {
                Line = lineColumn.Line,
                Column = lineColumn.Column,
                FileName = "dummy.cs",
                Buffer = source.Replace("$", ""),
                Type = testType
            };

            var bufferFilter = new UpdateBufferFilter(workspace);
            bufferFilter.OnActionExecuting(TestHelpers.CreateActionExecutingContext(request, controller));
            var testCommand = await controller.GetTestCommand(request);
            var command = testCommand.TestCommand;
            // strip off the path to k or dnx
            return command.Substring(command.IndexOf(" ") + 1).Trim();
        }
    }
}