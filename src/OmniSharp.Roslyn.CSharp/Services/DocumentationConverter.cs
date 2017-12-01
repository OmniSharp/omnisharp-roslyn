using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Xml;
using OmniSharp.Models.TypeLookup;

namespace OmniSharp.Roslyn.CSharp.Services.Documentation
{
    public static class DocumentationConverter
    {
        /// <summary>
        /// Converts the xml documentation string into a plain text string.
        /// </summary>
        public static string ConvertDocumentation(string xmlDocumentation, string lineEnding)
        {
            if (string.IsNullOrEmpty(xmlDocumentation))
                return string.Empty;

            var reader = new StringReader("<docroot>" + xmlDocumentation + "</docroot>");
            using (var xml = XmlReader.Create(reader))
            {
                var ret = new StringBuilder();

                try
                {
                    xml.Read();
                    string elementName = null;
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
                                    ret.Append(lineEnding);
                                    ret.Append("Remarks:");
                                    ret.Append(lineEnding);
                                    break;
                                case "example":
                                    ret.Append(lineEnding);
                                    ret.Append("Example:");
                                    ret.Append(lineEnding);
                                    break;
                                case "exception":
                                    ret.Append(lineEnding);
                                    ret.Append(GetCref(xml["cref"]).TrimEnd());
                                    ret.Append(": ");
                                    break;
                                case "returns":
                                    ret.Append(lineEnding);
                                    ret.Append("Returns: ");
                                    break;
                                case "see":
                                    ret.Append(GetCref(xml["cref"]));
                                    ret.Append(xml["langword"]);
                                    break;
                                case "seealso":
                                    ret.Append(lineEnding);
                                    ret.Append("See also: ");
                                    ret.Append(GetCref(xml["cref"]));
                                    break;
                                case "paramref":
                                    ret.Append(xml["name"]);
                                    ret.Append(" ");
                                    break;
                                case "param":
                                    ret.Append(lineEnding);
                                    ret.Append(TrimMultiLineString(xml["name"], lineEnding));
                                    ret.Append(": ");
                                    break;
                                case "value":
                                    ret.Append(lineEnding);
                                    ret.Append("Value: ");
                                    ret.Append(lineEnding);
                                    break;
                                case "br":
                                case "para":
                                    ret.Append(lineEnding);
                                    break;
                            }
                        }
                        else if (xml.NodeType == XmlNodeType.Text)
                        {
                            if (elementName == "code")
                            {
                                ret.Append(xml.Value);
                            }
                            else
                            {
                                ret.Append(TrimMultiLineString(xml.Value, lineEnding));
                            }
                        }
                    } while (xml.Read());
                }
                catch (Exception)
                {
                    return xmlDocumentation;
                }
                return ret.ToString();
            }
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

        public static DocumentationComment GetStructuredDocumentation(string xmlDocumentation, string lineEnding)
        {
            if (string.IsNullOrEmpty(xmlDocumentation))
                return null;
            var docComment = new DocumentationComment();
            var reader = new StringReader("<docroot>" + xmlDocumentation + "</docroot>");
            StringBuilder remarksText = new StringBuilder();
            StringBuilder exampleText = new StringBuilder();
            StringBuilder returnsText = new StringBuilder();
            StringBuilder summaryText = new StringBuilder();
            StringBuilder valueText = new StringBuilder();
            List<StringBuilder> paramElements = new List<StringBuilder>();
            List<StringBuilder> typeParamElements = new List<StringBuilder>();
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
                                    remarksText.Append("Remarks: ");
                                    currentSectionBuilder = remarksText;
                                    break;
                                case "example":
                                    exampleText.Append("Example: ");
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
                                    returnsText.Append("Returns: ");
                                    currentSectionBuilder = returnsText;
                                    break;
                                case "summary":
                                    summaryText.Append("Summary: ");
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

                docComment.RemarksText = remarksText.ToString();
                docComment.ExampleText = exampleText.ToString();
                docComment.ReturnsText = returnsText.ToString();
                docComment.SummaryText = summaryText.ToString();
                docComment.ValueText = valueText.ToString();
                docComment.ParamElements = paramElements.Select(s => s.ToString()).ToArray();
                docComment.TypeParamElements = typeParamElements.Select(s => s.ToString()).ToArray();
                docComment.Exception = exception.Select(s => s.ToString()).ToArray();
                return docComment;
            }
        }
    }
}

