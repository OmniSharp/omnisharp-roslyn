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
        public string[] TypeParamElements { get; }
        public string[] ParamElements { get; }
        public string ReturnsText { get; }
        public string RemarksText { get; }
        public string ExampleText { get; }
        public string ValueText { get; }
        public string[ ] Exception { get; }

        private DocumentationComment(string summaryText, string[] typeParamElements, string[] paramElements, string returnsText, string remarksText, string exampleText, string valueText, string [ ] exception)
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
            List<StringBuilder> typeParamElements = new List<StringBuilder>();
            List<StringBuilder> paramElements = new List<StringBuilder>();
            StringBuilder returnsText = new StringBuilder();
            StringBuilder remarksText = new StringBuilder();
            StringBuilder exampleText = new StringBuilder();
            StringBuilder valueText = new StringBuilder();
            List<StringBuilder> exception = new List<StringBuilder>();

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
                                    StringBuilder ExceptionInstance = new StringBuilder();
                                    ExceptionInstance.Append(GetCref(xml["cref"]).TrimEnd());
                                    ExceptionInstance.Append(": ");
                                    currentSectionBuilder = ExceptionInstance;
                                    exception.Add(ExceptionInstance);
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
                                    StringBuilder paramInstance = new StringBuilder();
                                    paramInstance.Append(TrimMultiLineString(xml["name"], lineEnding));
                                    paramInstance.Append(": ");
                                    currentSectionBuilder = paramInstance;
                                    paramElements.Add(paramInstance);
                                    break;
                                case "typeparamref":
                                    currentSectionBuilder.Append(xml["name"]);
                                    currentSectionBuilder.Append(" ");
                                    break;
                                case "typeparam":
                                    StringBuilder typeParamInstance = new StringBuilder();
                                    typeParamInstance.Append(TrimMultiLineString(xml["name"], lineEnding));
                                    typeParamInstance.Append(": ");
                                    currentSectionBuilder = typeParamInstance;
                                    typeParamElements.Add(typeParamInstance);
                                    break;
                                case "value":
                                    valueText.Append("Value: ");
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
            return new DocumentationComment(summaryText.ToString(), typeParamElements.Select(s => s.ToString()).ToArray(), paramElements.Select(s => s.ToString()).ToArray(), returnsText.ToString(), remarksText.ToString(), exampleText.ToString(), valueText.ToString(), exception.Select(s => s.ToString()).ToArray());
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
}
