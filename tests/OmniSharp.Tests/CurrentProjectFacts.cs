using System.Collections.Generic;


using OmniSharp.Models;
using Xunit;
using OmniSharp.AspNet5;

namespace OmniSharp.Tests
{
    public class CurrentProjectFacts
    {
        [Fact]
        public void CanFindInterfaceTypeImplementation()
        {
            var commands = GetCommands();

            Assert.Equal(1, commands.Count);
        }

        private IDictionary<string, string> GetCommands(string source = "")
        {
            var workspace = TestHelpers.CreateSimpleWorkspace(source);
            var aspnet5Context = GetAspNet5Context();
            var controller = new CurrentProjectController(aspnet5Context,null, workspace);
            var request = CreateRequest(source);
            var response = controller.CurrentProject(request);
            return response.Commands;
        }

        private AspNet5Context GetAspNet5Context()
        {
            var context = new AspNet5Context();
            var projectCounter = 1;
            context.Projects.Add(projectCounter, new Project
            {
                Path = "project.json",
                Commands = { { "kestrel", "Microsoft.AspNet.Hosting --server Kestrel" } }
            });

            return context;
        }

        private Request CreateRequest(string source, string fileName = "dummy.cs")
        {
            return new Request
            {
                Line = 0,
                Column = 0,
                FileName = fileName,
                Buffer = source
            };
        }

    }
}