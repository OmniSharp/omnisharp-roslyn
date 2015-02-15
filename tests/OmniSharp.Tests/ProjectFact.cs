using OmniSharp.AspNet5;
using OmniSharp.Models;
using System.Collections.Generic;
using Xunit;

namespace OmniSharp.Tests
{
    public class CurrentProjectFacts
    {
        [Fact]
        public void ProjectCommands()
        {
            var commands = GetCommands();

            Assert.Equal(1, commands.Count);
        }

        private IDictionary<string, string> GetCommands(string source = "")
        {
            var workspace = TestHelpers.CreateSimpleWorkspace(source);
            var aspnet5Context = GetAspNet5Context();
            var controller = new ProjectSystemController(aspnet5Context, null, workspace);
            var request = CreateRequest(source);
            var response = controller.CurrentProject(request);
            if (response.AspNet5Project != null)
            {
                return response.AspNet5Project.Commands;
            }
            return new Dictionary<string, string>();
        }

        private AspNet5Context GetAspNet5Context()
        {
            var context = new AspNet5Context();
            var projectCounter = 1;
            context.Projects.Add(projectCounter, new Project
            {
                Name = "OmniSharp",
                Path = "project.json",
                Commands = { { "kestrel", "Microsoft.AspNet.Hosting --server Kestrel" } }
            });
            context.ProjectContextMapping.Add("project.json", 1);

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
