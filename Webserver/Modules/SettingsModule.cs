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
            {"xhtml", "application/xhtml+xml"},
            {"woff", "application/font-woff"},
            {"ttf", "application/x-font-truetype"},
            {"svg", "image/svg+xml"},
            {"txt", "text/plain"}
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
        public void SetWebPort(int portNumber)
        {
            xmlModule.setElement("WebPortNumber", portNumber.ToString());
        }

        public int GetWebPort()
        {
            String portNumber = xmlModule.getElement("WebPortNumber");
            if (portNumber == null)
                return 0;
            else
                return int.Parse(portNumber);
        }

        public void SetControlPort(int portNumber)
        {
            xmlModule.setElement("ControlPortNumber", portNumber.ToString());
        }

        public int GetControlPort()
        {
            String controlPortNumber = xmlModule.getElement("ControlPortNumber");
            if (controlPortNumber == null)
                return 0;
            else
                return int.Parse(controlPortNumber);
        }

        public void SetWebroot(string rootDirectory)
        {
            xmlModule.setElement("WebrootDirectory", rootDirectory);
        }

        public string GetWebroot()
        {
            return xmlModule.getElement("WebrootDirectory");
        }

        public void SetDefaultPage(String defaultPages)
        {
            xmlModule.setElement("DefaultPage", defaultPages);
        }

        public List<string> GetDefaultPage()
        {
            String defaultPages = xmlModule.getElement("DefaultPage");
            return defaultPages.Split(';').ToList();
        }

        public Boolean GetAllowedDirectoryBrowsing()
        {
            return Convert.ToBoolean(xmlModule.getElement("DirectoryTraversal"));
        }

        public void SetAllowedDirectoryBrowsing(Boolean dirBrowsing)
        {
            xmlModule.setElement("DirectoryTraversal", dirBrowsing.ToString());
        }

        public List<string> GetControlDefaultPage()
        {
            List<string> defaultPages = new List<string>();
            defaultPages.Add("login.html");
            defaultPages.Add("login.htm");

            return defaultPages;
        }

        public void SaveSettings()
        {
            xmlModule.Save();
            OnSettingsUpdated();
        }

        public event EventHandler<Boolean> SettingsUpdated;
        
        protected virtual void OnSettingsUpdated()
        {
            EventHandler<Boolean> handler = SettingsUpdated;
            if (handler != null)
                handler(this, true);
        }

#endregion

        #region Private Server Settings
        public Dictionary<string, string> getAllowedMIMETypes()
        {
            return xmlModule.getMimeTypeDictionary();
        }

        public List<string> getAllowedVirtualDirs()
        {
            throw new NotImplementedException();
        }

        public Dictionary<string, string> GetSettings()
        {
            Dictionary<String, String> dSettings = new Dictionary<String, String>();

            dSettings.Add("webport", GetWebPort().ToString());
            dSettings.Add("controlport", GetControlPort().ToString());
            dSettings.Add("webroot", GetWebroot());
            dSettings.Add("defaultpage", xmlModule.getElement("DefaultPage"));

            String sAllowedBrowsing = GetAllowedDirectoryBrowsing() ? "checked" : "";
            dSettings.Add("directorybrowsing", sAllowedBrowsing);

            return dSettings;
        }
        #endregion
    }
}
