using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Webserver.Interfaces;

namespace Webserver.Modules
{
    public class SettingsModule : IPublicSettingsModule, IServerSettingsModule
    {
        private static String settingsLocation = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, @"Data\settings.xml");
        private static Dictionary<String, String> defaultSettings = new Dictionary<String, String>() 
        {
            {"WebPortNumber", "8000"},
            {"ControlPortNumber", "8001"},
            {"WebrootDirectory", @"C:\webserver\www"},
            {"DefaultPage", "index.html;index.htm"},
            {"DirectoryTraversal", "false"}
        };

        private static Dictionary<String, String> allowedMIMETypes = new Dictionary<String, String>()
        {
            {"bmp", "image/bmp"},
            {"css", "text/css"},
            {"gif", "image/gif"},
            {"htm", "text/html"},
            {"html", "text/html"},
            {"jpe", "image/jpeg"},
            {"jpeg", "image/jpeg"},
            {"jpg", "image/jpeg"},
            {"js", "application/x-javascript"},
            {"png", "image/png"},
            {"xhtml", "application/xhtml+xml"}
        };

        private XmlModule xmlModule;

        public SettingsModule()
        {
            xmlModule = new XmlModule();
            if (!xmlModule.setXmlDocument(settingsLocation))
            {
                xmlModule.createSettingsXML(settingsLocation, defaultSettings, allowedMIMETypes);
            }
        }

        #region Public Settings
        public void setWebPort(int portNumber)
        {
            xmlModule.setElement("WebPortNumber", portNumber.ToString());
        }

        public int getWebPort()
        {
            String portNumber = xmlModule.getElement("WebPortNumber");
            if (portNumber == null)
                return 0;
            else
                return int.Parse(portNumber);
        }

        public void setControlPort(int portNumber)
        {
            xmlModule.setElement("ControlPortNumber", portNumber.ToString());
        }

        public int getControlPort()
        {
            throw new NotImplementedException();
        }

        public void setWebroot(string rootDirectory)
        {
            throw new NotImplementedException();
        }

        public string getWebroot()
        {
            throw new NotImplementedException();
        }

        public void setDefaultPage(List<string> defaultPages)
        {
            throw new NotImplementedException();
        }

        public List<string> getDefaultPage()
        {
            throw new NotImplementedException();
        }

        public bool saveSettings()
        {
            throw new NotImplementedException();
        }
#endregion

        #region Private Server Settings
        public Dictionary<string, string> getAllowedMIMETypes()
        {
            throw new NotImplementedException();
        }

        public List<string> getAllowedVirtualDirs()
        {
            throw new NotImplementedException();
        }
        #endregion
    }
}
