using OmniSharp.Models.v2.TypeLookUp;
using System;
using System.IO;
using System.Linq;
using System.Text;
using System.Xml;

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

        public static DocumentationComment ConvertDocumentationObject(string xmlDocumentation, string lineEnding)
        {
            if (string.IsNullOrEmpty(xmlDocumentation))
                return null;
            var docComment = new DocumentationComment();
            var reader = new StringReader("<docroot>" + xmlDocumentation + "</docroot>");
            using (var xml = XmlReader.Create(reader))
            {
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
                                    docComment.RemarksText.Append("Remarks: ");
                                    docComment.RemarksText.Append(ReadInnerXMLText(xml, elementName, lineEnding));
                                    break;
                                case "example":
                                    docComment.ExampleText.Append("Example: ");
                                    docComment.ExampleText.Append(ReadInnerXMLText(xml, elementName, lineEnding));
                                    break;
                                case "exception":
                                    docComment.ExceptionText.Append(GetCref(xml["cref"]).TrimEnd());
                                    docComment.ExceptionText.Append(": ");
                                    docComment.ExceptionText.Append(ReadInnerXMLText(xml, elementName, lineEnding));
                                    break;
                                case "returns":
                                    docComment.ReturnsText.Append("Returns: ");
                                    docComment.ReturnsText.Append(ReadInnerXMLText(xml, elementName, lineEnding));
                                    break;
                                case "summary":
                                    docComment.SummaryText.Append("Summary: ");
                                    docComment.SummaryText.Append(ReadInnerXMLText(xml, elementName, lineEnding));
                                    break;
                                /*case "see":
                                    docComment.Append(GetCref(xml["cref"]));
                                    docComment.Append(xml["langword"]);
                                    break;
                                case "seealso":
                                    docComment.Append(lineEnding);
                                    docComment.Append("See also: ");
                                    docComment.Append(GetCref(xml["cref"]));
                                    break;
                                case "paramref":
                                    docComment.paramref.Append(xml["name"]);
                                    docComment.paramref.Append(" ");
                                    break;*/
                                case "param":
                                    StringBuilder parameter = new StringBuilder();
                                    parameter.Append(TrimMultiLineString(xml["name"], lineEnding));
                                    parameter.Append(": ");
                                    parameter.Append(ReadInnerXMLText(xml, elementName, lineEnding));
                                    docComment.Param.Add(parameter);
                                    break;
                                case "value":
                                    docComment.value.Append(lineEnding);
                                    docComment.value.Append("Value: ");
                                    docComment.value.Append(lineEnding);
                                    //docComment.value.Append(TrimMultiLineString(xml.ReadInnerXml(), lineEnding));
                                    break;
                                case "br":
                                case "para":
                                    //docComment.Append(lineEnding);
                                    break;
                            }
                        }
                        /*else if (xml.NodeType == XmlNodeType.Text)
                        {
                            if (elementName == "code")
                            {
                                docComment.Append(xml.Value);
                            }
                            else
                            {
                                docComment.Append(TrimMultiLineString(xml.Value, lineEnding));
                            }
                        }*/
                    } while (xml.Read());
                }
                catch (Exception)
                {
                    return null;
                }
                return docComment;
            }
        }
        private static string ReadInnerXMLText(XmlReader xml, string elementName,string lineEnding)
        {
            if (xml.Read())
            {
                if (xml.NodeType == XmlNodeType.Text)
                {
                    if (elementName == "code")
                    {
                        return xml.Value;
                    }
                    else
                    {
                        return TrimMultiLineString(xml.Value, lineEnding);
                    }
                }
            }
            return "";
        }
    }
}

