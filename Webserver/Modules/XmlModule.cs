using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Permissions;
using System.Text;
using System.Threading.Tasks;
using System.Xml;

namespace Webserver.Modules
{
    class XmlModule
    {
        private XmlDocument doc;
        private String filePath;

        public XmlModule()
        {
            doc = new XmlDocument();
        }

        public Boolean setXmlDocument(String filePath)
        {
            try
            {
                doc.Load(filePath);
                this.filePath = filePath;
            }
            catch(Exception e)
            {
                return false;
            }
            return true;
        }

        public String getElement(String elementName)
        {
            XmlNode node = doc.DocumentElement.SelectSingleNode("//" + elementName);
            if (node != null)
                return node.InnerText;
            else
                return null;
        }

        public Dictionary<string, string> getMimeTypeDictionary()
        {
            XmlNode node = doc.DocumentElement.SelectSingleNode("//AllowedMIMETypes");
            if (node != null)
            {
                Dictionary<string, string> mimeTypeDictionary = new Dictionary<string, string>();
                foreach (XmlNode mimeNode in node.ChildNodes)
                {
                    String mimeFile = mimeNode.SelectSingleNode("FileName").InnerText;
                    String mimeText = mimeNode.SelectSingleNode("MimeText").InnerText;
                    mimeTypeDictionary.Add(mimeFile, mimeText);
                }

                return mimeTypeDictionary;
            }
            else
                return null;
        }

        public void setElement(String elementName, String value)
        {
            XmlNode node = doc.DocumentElement.SelectSingleNode("//" + elementName);
            if (node != null)
                node.FirstChild.InnerText = value;
        }

        public void Save()
        {
            doc.Save(filePath);
        }

        public void createSettingsXML(String filepath, Dictionary<String, String> settings, Dictionary<String, String> MIMETypes)
        {
            if (!Directory.Exists(Path.GetDirectoryName(filepath)))
                Directory.CreateDirectory(Path.GetDirectoryName(filepath));

            XmlElement docRoot = doc.CreateElement("Settings");
            doc.AppendChild(docRoot);

            XmlDeclaration xmlDeclaration = doc.CreateXmlDeclaration("1.0", "UTF-8", null);
            doc.InsertBefore(xmlDeclaration, doc.DocumentElement);

            // Add server settings
            foreach (KeyValuePair<String, String> setting in settings)
            {
                XmlElement settingElement   = doc.CreateElement(setting.Key);
                XmlText settingText         = doc.CreateTextNode(setting.Value);
                settingElement.AppendChild(settingText);

                docRoot.AppendChild(settingElement);
            }

            // Add allowed MIME types
            XmlElement allowedTypes = doc.CreateElement("AllowedMIMETypes");

            foreach(KeyValuePair<String, String> mime in MIMETypes)
            {
                XmlElement mimeElement  = doc.CreateElement("MimeItem");
                
                XmlElement mimeFileName = doc.CreateElement("FileName");
                XmlText fileNameText    = doc.CreateTextNode(mime.Key);
                mimeFileName.AppendChild(fileNameText);
                mimeElement.AppendChild(mimeFileName);

                XmlElement mimeTypeText = doc.CreateElement("MimeText");
                XmlText mimeText        = doc.CreateTextNode(mime.Value);
                mimeTypeText.AppendChild(mimeText);
                mimeElement.AppendChild(mimeTypeText);

                allowedTypes.AppendChild(mimeElement);
            }
            docRoot.AppendChild(allowedTypes);
            doc.Save(filepath);
        }
    }
}
