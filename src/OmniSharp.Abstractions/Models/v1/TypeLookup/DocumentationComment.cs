using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Xml;

namespace OmniSharp.Models.TypeLookup
{
    public class DocumentationComment
    {
        public string SummaryText { get; }
        public DocumentedObject[] TypeParamElements { get; }
        public DocumentedObject[] ParamElements { get; }
        public string ReturnsText { get; }
        public string RemarksText { get; }
        public string ExampleText { get; }
        public string ValueText { get; }
        public DocumentedObject[ ] Exception { get; }

        private DocumentationComment(string summaryText, DocumentedObject[] typeParamElements, DocumentedObject[] paramElements, string returnsText, string remarksText, string exampleText, string valueText, DocumentedObject[ ] exception)
        {
            SummaryText = summaryText;
            TypeParamElements = typeParamElements;
            ParamElements = paramElements;
            ReturnsText = returnsText;
            RemarksText = remarksText;
            ExampleText = exampleText;
            ValueText = valueText;
            Exception = exception;
        }

        public static DocumentationComment From(string xmlDocumentation, string lineEnding)
        {
            var reader = new StringReader("<docroot>" + xmlDocumentation + "</docroot>");
            StringBuilder summaryText = new StringBuilder();
            List<DocumentedObjectHelper> typeParamElements = new List<DocumentedObjectHelper>();
            List<DocumentedObjectHelper> paramElements = new List<DocumentedObjectHelper>();
            StringBuilder returnsText = new StringBuilder();
            StringBuilder remarksText = new StringBuilder();
            StringBuilder exampleText = new StringBuilder();
            StringBuilder valueText = new StringBuilder();
            List<DocumentedObjectHelper> exception = new List<DocumentedObjectHelper>();

            using (var xml = XmlReader.Create(reader))
            {
                try
                {
                    xml.Read();
                    string elementName = null;
                    StringBuilder currentSectionBuilder = null;
                    do
                    {
                        if (xml.NodeType == XmlNodeType.Element)
                        {
                            elementName = xml.Name.ToLowerInvariant();
                            switch (elementName)
                            {
                                case "filterpriority":
                                    xml.Skip();
                                    break;
                                case "remarks":
                                    currentSectionBuilder = remarksText;
                                    break;
                                case "example":
                                    currentSectionBuilder = exampleText;
                                    break;
                                case "exception":
                                    DocumentedObjectHelper exceptionInstance = new DocumentedObjectHelper();
                                    exceptionInstance.Name = GetCref(xml["cref"]).TrimEnd();
                                    currentSectionBuilder = exceptionInstance.Documentation;
                                    exception.Add(exceptionInstance);
                                    break;
                                case "returns":
                                    currentSectionBuilder = returnsText;
                                    break;
                                case "summary":
                                    currentSectionBuilder = summaryText;
                                    break;
                                case "see":
                                    currentSectionBuilder.Append(GetCref(xml["cref"]));
                                    currentSectionBuilder.Append(xml["langword"]);
                                    break;
                                case "seealso":
                                    currentSectionBuilder.Append("See also: ");
                                    currentSectionBuilder.Append(GetCref(xml["cref"]));
                                    break;
                                case "paramref":
                                    currentSectionBuilder.Append(xml["name"]);
                                    currentSectionBuilder.Append(" ");
                                    break;
                                case "param":
                                    DocumentedObjectHelper paramInstance = new DocumentedObjectHelper();
                                    paramInstance.Name = TrimMultiLineString(xml["name"], lineEnding);
                                    currentSectionBuilder = paramInstance.Documentation;
                                    paramElements.Add(paramInstance);
                                    break;
                                case "typeparamref":
                                    currentSectionBuilder.Append(xml["name"]);
                                    currentSectionBuilder.Append(" ");
                                    break;
                                case "typeparam":
                                    DocumentedObjectHelper typeParamInstance = new DocumentedObjectHelper();
                                    typeParamInstance.Name = TrimMultiLineString(xml["name"], lineEnding);
                                    currentSectionBuilder = typeParamInstance.Documentation;
                                    typeParamElements.Add(typeParamInstance);
                                    break;
                                case "value":
                                    currentSectionBuilder = valueText;
                                    break;
                                case "br":
                                case "para":
                                    currentSectionBuilder.Append(lineEnding);
                                    break;
                            }
                        }
                        else if (xml.NodeType == XmlNodeType.Text && currentSectionBuilder != null)
                        {
                            if (elementName == "code")
                            {
                                currentSectionBuilder.Append(xml.Value);
                            }
                            else
                            {
                                currentSectionBuilder.Append(TrimMultiLineString(xml.Value, lineEnding));
                            }
                        }
                    } while (xml.Read());
                }
                catch (Exception)
                {
                    return null;
                }
            }

            return new DocumentationComment(summaryText.ToString(), typeParamElements.Select(s => s.ConvertToDocumentedObject()).ToArray(), paramElements.Select(s => s.ConvertToDocumentedObject()).ToArray(), returnsText.ToString(), remarksText.ToString(), exampleText.ToString(), valueText.ToString(), exception.Select(s => s.ConvertToDocumentedObject()).ToArray());
        }

        private static string TrimMultiLineString(string input, string lineEnding)
        {
            var lines = input.Split(new string[] { "\n", "\r\n" }, StringSplitOptions.RemoveEmptyEntries);
            return string.Join(lineEnding, lines.Select(l => l.TrimStart()));
        }

        private static string GetCref(string cref)
        {
            if (cref == null || cref.Trim().Length == 0)
            {
                return "";
            }
            if (cref.Length < 2)
            {
                return cref;
            }
            if (cref.Substring(1, 1) == ":")
            {
                return cref.Substring(2, cref.Length - 2) + " ";
            }
            return cref + " ";
        }
    }

    class DocumentedObjectHelper
    {
        public string Name { get; set; }
        public StringBuilder Documentation { get; set; }

        public  DocumentedObjectHelper()
        {
            Documentation = new StringBuilder();
        }

        public  DocumentedObject ConvertToDocumentedObject()
        {
            var documentedObject = new DocumentedObject
            {
                Name = Name,
                Documentation = Documentation.ToString()
            };
            return documentedObject;
        }
    }
}
