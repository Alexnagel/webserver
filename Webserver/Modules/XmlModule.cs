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
        private XmlDocument reader;
        private Dictionary<String, String> defaultSettings = new Dictionary<String, String>() 
        {
            {"WebPortNumber", "8000"},
            {"ControlPortNumber", "8001"},
            {"WebrootDirectory", "C:\\webserver\\www"},
            {"DefaultPage", "index.html;index.htm"},
            {"AllowedMimeTypes", ""}
        };

        public XmlModule()
        {
            reader = new XmlDocument();
        }

        public Boolean setXmlDocument(String filePath)
        {
            try
            {
                reader.Load(filePath);
            }
            catch(Exception e)
            {
                return false;
            }
            return true;
        }

        public String getElement(String elementName)
        {
            XmlNode node = reader.DocumentElement.SelectSingleNode("//" + elementName);
            if (node != null)
                return node.Value;
            else
                return null;
        }

        public void setElement(String elementName, String value)
        {
            XmlNode node = reader.DocumentElement.SelectSingleNode("//" + elementName);
            if (node != null)
                node.Value = value;
        }

        private void createSettingsXML(String filepath)
        {
            
        }
    }
}
