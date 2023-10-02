using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Xml;
using Windows.Devices.Enumeration;

public class XmlHelper
{
    // Creates a new element and appends it to the specified parent node.
    public static XmlElement AddElement(XmlDocument xmlDoc, XmlNode parentNode, string elementName, string textContent = null)
    {
        XmlElement newElement = xmlDoc.CreateElement(elementName);
        if (!string.IsNullOrEmpty(textContent))
        {
            newElement.InnerText = textContent;
        }
        parentNode.AppendChild(newElement);
        return newElement;
    }

    // Function to add a Param element with Value, Comment, and Key attributes
    public static void AddParam(XmlDocument xmlDoc, XmlElement parentElement, string value, string comment, string key)
    {
        XmlElement paramElement = xmlDoc.CreateElement("Param");
        AddAttribute(xmlDoc, paramElement, "Value", value);
        AddAttribute(xmlDoc, paramElement, "Comment", comment);
        AddAttribute(xmlDoc, paramElement, "Key", key);
        parentElement.AppendChild(paramElement);
    }

    // Function to add an attribute to an XML element
    public static void AddAttribute(XmlDocument xmlDoc, XmlElement element, string attributeName, string attributeValue)
    {
        XmlAttribute newAttribute = xmlDoc.CreateAttribute(attributeName);
        newAttribute.Value = attributeValue;
        element.Attributes.Append(newAttribute);
    }

    public static string GetFormattedXmlString(XmlDocument xmlDoc)
    {
        using (StringWriter sw = new StringWriter())
        {
            XmlWriterSettings settings = new XmlWriterSettings
            {
                Indent = true,
                IndentChars = " ", // Dört boşlukla bir satır atlamak için
                Encoding = Encoding.UTF8, // İsteğe bağlı: UTF-8 karakter kodlaması
                OmitXmlDeclaration = true // İsteğe bağlı: XML beyanını çıkarır
            };

            using (XmlWriter writer = XmlWriter.Create(sw, settings))
            {
                xmlDoc.Save(writer);
            }

            return sw.ToString();
        }
    }

    public static bool ElementExists(XmlNode parent, string elementName)
    {
        XmlNode existingNode = parent.SelectSingleNode(elementName);
        return existingNode != null;
    }

}
