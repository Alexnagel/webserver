using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Webserver.Modules
{
    class FileModule
    {
        private SettingsModule _settingsModule;
        private Dictionary<String, String> _allowedMimeTypes;

        public FileModule()
        {
            _settingsModule     = new SettingsModule();
            _allowedMimeTypes   = _settingsModule.getAllowedMIMETypes();
        }

        public String GetDefaultPage(String localPath)
        {
            List<String> defaultPages = _settingsModule.getDefaultPage();
            foreach (String defaultPage in defaultPages)
            {
                if (File.Exists(Path.Combine(localPath, defaultPage)))
                {
                    return defaultPage;
                }
            }
            return "";
        }

        public String GetControlDefaultPage(String localPath)
        {
            List<String> defaultPages = _settingsModule.getControlDefaultPage();
            foreach (String defaultPage in defaultPages)
            {
                if (File.Exists(Path.Combine(localPath, defaultPage)))
                {
                    return defaultPage;
                }
            }
            return "";
        }

        public String CombinePaths(params string[] PathsToCombine)
        {
            string[] trimmedPaths = new string[PathsToCombine.Length];
            for (int i = 0; i < PathsToCombine.Length; i++)
            {
                trimmedPaths[i] = PathsToCombine[i].TrimStart('/');
            }
            return Path.Combine(trimmedPaths);
        }

        public String GetLocalPath(String sRequestedDirectory)
        {
            String webServerRoot = _settingsModule.getWebroot();

            // Remove spaces and lower case
            sRequestedDirectory.Trim();
            sRequestedDirectory = sRequestedDirectory.ToLower();

            String localPath = webServerRoot;
            if (!sRequestedDirectory.Equals("/"))
                localPath = Path.Combine(webServerRoot, sRequestedDirectory);

            if (!Directory.Exists(localPath))
                return "";
            else
                return localPath;
        }

        public String GetMimeType(String sRequestFile)
        {
            // Remove spaces and lower case
            sRequestFile.Trim();
            sRequestFile = sRequestFile.ToLower();

            String fileExtension = Path.GetExtension(sRequestFile);
            fileExtension = fileExtension.Replace(".", "");
            Boolean mimeTypeAllowed = _allowedMimeTypes.ContainsKey(fileExtension);

            // Check if mimetype exists
            if (!mimeTypeAllowed)
                return "";
            else
                return _allowedMimeTypes.Where(pair => pair.Key.Equals(fileExtension)).First().Value;
        }

        public Byte[] FileToBytes(String sPhysicalFilePath)
        {
            FileStream fs = null;
            if (File.Exists(sPhysicalFilePath))
            {
                fs = new FileStream(sPhysicalFilePath, FileMode.Open, FileAccess.Read, FileShare.Read);
            }
            else
            {
                return null;
            }

            // Create byteReader
            BinaryReader reader = new BinaryReader(fs);
            byte[] bytes = new byte[fs.Length];
            int read, iTotalBytes = 0;
            while ((read = reader.Read(bytes, 0, bytes.Length)) != 0)
            {
                iTotalBytes += read;
            }

            reader.Close();
            fs.Close();

            if (iTotalBytes == bytes.Length)
                return bytes;
            else
                return null;
        }
    }
}
