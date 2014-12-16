using Microsoft.CodeAnalysis;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Xml.Linq;

namespace OmniSharp.MSBuild.ProjectFile
{
    class ProjectItem
    {
        private XElement Element;
        public ProjectItem(XElement element)
        {
            Element = element;
        }
        public string EvaluatedInclude
        {
            get
            {
                return this.Element.Attribute("Include").Value;
            }
        }
        public bool HasMetadata(string name)
        {
            return Element.Descendants(Element.Document.Root.Name.Namespace + name).Any();
        }
        public string GetMetadataValue(string name)
        {
            return Element.Descendants(Element.Document.Root.Name.Namespace + name).First().Value;
        }
    }

    class Project
    {

        public string DirectoryPath { get; private set; }

        readonly XDocument document;

        public Project(string fileName)
        {
            DirectoryPath = Path.GetDirectoryName(fileName);
            var xml = File.ReadAllText(fileName);
            document = XDocument.Parse(xml);
        }

        public string GetPropertyValue(string name)
        {
            XElement element = document.Descendants(document.Root.Name.Namespace + "PropertyGroup").Descendants(document.Root.Name.Namespace + name).FirstOrDefault();
            return element == null ? string.Empty : element.Value;
        }

        public ICollection<ProjectItem> GetItems(string itemType)
        {
            IEnumerable<XElement> elements = document.Descendants(document.Root.Name.Namespace + "ItemGroup").Descendants(document.Root.Name.Namespace + itemType);
            return (from element in elements select new ProjectItem(element)).ToList();
        }
    }

    public class ProjectFile
    {
        public ProjectId ProjectId { get; private set; }

        public Guid Id { get; private set; }

        public string Name { get; private set; }

        public string Filepath { get; private set; }

        public string AssemblyName { get; private set; }

        public IEnumerable<string> SourceFiles { get; private set; }

        public IEnumerable<Tuple<Guid, string>> ProjectReferences { get; private set; }

        public IEnumerable<Tuple<string, string>> References { get; private set; }

        public ProjectFile(Guid id, string name, string path)
        {
        ProjectId = ProjectId.CreateNewId();
            Id = id;
            Name = name;
            Filepath = path;

            var directory = Path.GetDirectoryName(path);
            var project = new Project(path);

            AssemblyName = project.GetPropertyValue("AssemblyName");

            SourceFiles = project.GetItems("Compile").Select(item => {
                // todo - support glob patterns
                return Path.Combine(directory, item.EvaluatedInclude);
            });

            ProjectReferences = project.GetItems("ProjectReference").Select(item =>
            {
                return Tuple.Create(Guid.Parse(item.GetMetadataValue("Project")), item.GetMetadataValue("Name"));
            });

            var references = new List<Tuple<string, string>>();
            references.Add(Tuple.Create<string, string>("mscorlib", null));
            references.AddRange(project.GetItems("Reference").Select(item => {
                return item.HasMetadata("HintPath") 
                    ? Tuple.Create(item.EvaluatedInclude, item.GetMetadataValue("HintPath")) 
                    : Tuple.Create(item.EvaluatedInclude, (string) null);   
            }));
            References = references;
        }
    }
}