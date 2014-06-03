using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Xml;

namespace Webserver.Modules
{
    class XmlModule
    {
        private XmlDocument doc;

        public XmlModule()
        {
            doc = new XmlDocument();
        }

        public Boolean setXmlDocument(String filePath)
        {
            try
            {
                doc.Load(filePath);
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
                return node.Value;
            else
                return null;
        }

        public void setElement(String elementName, String value)
        {
            XmlNode node = doc.DocumentElement.SelectSingleNode("//" + elementName);
            if (node != null)
                node.Value = value;
        }

        public void createSettingsXML(String filepath, Dictionary<String, String> settings, Dictionary<String, String> MIMETypes)
        {
            XmlDeclaration xmlDeclaration = doc.CreateXmlDeclaration("1.0", "UTF-8", null);
            doc.InsertBefore(xmlDeclaration, doc.DocumentElement);

            // Add server settings
            foreach (KeyValuePair<String, String> setting in settings)
            {
                XmlElement settingElement = doc.CreateElement(setting.Key);
                XmlText settingText = doc.CreateTextNode(setting.Value);
                settingElement.AppendChild(settingElement);

                doc.AppendChild(settingElement);
            }

            // Add allowed MIME types
            XmlElement allowedTypes = doc.CreateElement("AllowedMIMETypes");

            foreach(KeyValuePair<String, String> mime in MIMETypes)
            {
                XmlElement mimeElement = doc.CreateElement("MimeItem");
                
                XmlElement mimeFileName = doc.CreateElement("FileName");
                XmlText fileNameText = doc.CreateTextNode(mime.Key);
                mimeFileName.AppendChild(fileNameText);
                mimeElement.AppendChild(mimeFileName);

                XmlElement mimeTypeText = doc.CreateElement("MimeText");
                XmlText mimeText = doc.CreateTextNode(mime.Value);
                mimeFileName.AppendChild(mimeText);
                mimeElement.AppendChild(mimeTypeText);

                allowedTypes.AppendChild(mimeElement);
            }
            doc.AppendChild(allowedTypes);

            doc.Save(filepath);
        }
    }
}
