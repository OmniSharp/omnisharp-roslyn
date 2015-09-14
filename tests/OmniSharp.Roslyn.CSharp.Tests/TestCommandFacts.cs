using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;
using OmniSharp.Dnx;
using OmniSharp.Mef;
using OmniSharp.Models;
using OmniSharp.Options;
using OmniSharp.Roslyn.CSharp.Services.TestCommands;
using OmniSharp.Services;
using OmniSharp.Tests;
using Xunit;

namespace OmniSharp.Roslyn.CSharp.Tests
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

            Assert.EndsWith("test -method TestClass.ThisIsATest", testArgs);
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

            Assert.EndsWith("test -method TestClass.ThisIsATest", testArgs);
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

            Assert.EndsWith("test -method TestClass.ThisIsATest", testArgs);
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
            Assert.EndsWith("test -method TestClass.ThisIsATest", testArgs);
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
            Assert.EndsWith("test -method Namespace.Something.TestClass.ThisIsATest", testArgs);
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
            Assert.EndsWith("test -class TestClass", testArgs);
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
            Assert.EndsWith("test -class SomeNamespace.TestClass", testArgs);
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
            Assert.EndsWith("test -class SomeNamespace.TestClass", testArgs);
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
            Assert.EndsWith("test -class SomeNamespace.TestClass", testArgs);
        }

        private async Task<string> GetTestCommandArgumentsAsync(string source, TestCommandType testType = TestCommandType.Single)
        {
            var workspace = await TestHelpers.CreateSimpleWorkspace(source);
            var context = new DnxContext();
            var projectName = "project.json";
            var projectCounter = 1;

            context.ProjectContextMapping.Add(projectName, projectCounter);
            context.Projects.Add(projectCounter, new Project
            {
                Path = "project.json",
                Commands = { { "test", "Xunit.KRunner" } }
            });

            ITestCommandProvider testCommandProvider = new DnxTestCommandProvider(context, new FakeEnvironment(), new FakeLoggerFactory(), new NullEventEmitter());
            var controller = new TestCommandService(workspace, new [] { testCommandProvider });
            var lineColumn = TestHelpers.GetLineAndColumnFromDollar(source);

            var request = new TestCommandRequest
            {
                Line = lineColumn.Line,
                Column = lineColumn.Column,
                FileName = "dummy.cs",
                Buffer = source.Replace("$", ""),
                Type = testType
            };

            await workspace.BufferManager.UpdateBuffer(request);

            var testCommand = await controller.Handle(request);
            return testCommand.TestCommand;
        }
    }
}
