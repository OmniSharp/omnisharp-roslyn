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
        private Project _project;

        public Guid ProjectId { get; private set; }

        public string Name { get; private set; }

        public string Path { get; private set; }

        public ProjectFileInfo Info {
            get
            {
                var references = new List<string>();
                references.Add("mscorlib");
                references.AddRange(_project.GetItems("References").Select(item => item.EvaluatedInclude));

                var ret = new ProjectFileInfo(ProjectId, Name, Path,
                    _project.GetPropertyValue("AssemblyName"),
                    _project.GetItems("Compile").Select(item => item.EvaluatedInclude),
                    references);

                return ret;
            }
        }

        public ProjectFile(Guid projectId, string name, string path)
        {
            _project = new Project(path);
            ProjectId = projectId;
            Name = name;
            Path = path;
        }

    }
}