using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Threading;
using System.Xml.Linq;
using Microsoft.CodeAnalysis;

namespace OmniSharp.Roslyn
{
    public class XmlDocumentationProvider : DocumentationProvider
    {
        private Lazy<Dictionary<string, string>> _docComments;
        private readonly string _path;

        public XmlDocumentationProvider(string path)
        {
            _path = path;
            _docComments = new Lazy<Dictionary<string, string>>(() =>
            {
                var comments = new Dictionary<string, string>();
                using (var stream = File.OpenRead(path))
                {
                    var doc = XDocument.Load(stream);
                    foreach (var e in doc.Descendants("member"))
                    {
                        if (e.Attribute("name") != null)
                        {
                            comments[e.Attribute("name").Value] = e.ToString();
                        }
                    }
                }

                return comments;
            });
        }

        protected override string GetDocumentationForSymbol(string documentationMemberID, CultureInfo preferredCulture, CancellationToken cancellationToken = default(CancellationToken))
        {
            string docComment;
            return _docComments.Value.TryGetValue(documentationMemberID, out docComment) ? docComment : "";
        }

        public override bool Equals(object obj)
        {
            var other = obj as XmlDocumentationProvider;
            return other != null && _path.Equals(other._path);
        }

        public override int GetHashCode()
        {
            return _path.GetHashCode();
        }
    }
}