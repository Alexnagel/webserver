using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Security;
using System.Net.Sockets;
using System.Security.Authentication;
using System.Security.Cryptography.X509Certificates;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Webserver.Interfaces;
using Webserver.Modules;

namespace Webserver
{
    class ControlServer
    {
        private const string CERTIFICATE_PATH = @"Data\webserverCA.pfx";
        private const string CERTIFICATE_PASSWORD = "webserver";

        private static IPAddress _serverIP;
        private static int       _listenPort;

        // settings modules
        private IPublicSettingsModule _publicSettingsModule;
        private IServerSettingsModule _serverSettingsModule;

        // Dictionary for all mimetypes
        private Dictionary<string, string> _allowedMimeTypes;

        private TcpListener _tcpListener;
        private Boolean     _isRunning;
        
        private X509Certificate certificate;

        public ControlServer(IPublicSettingsModule settingsModule)
        {
            // Connect DB
            new MySqlModule();
            // set the settingsmodules
            _publicSettingsModule = settingsModule;
            _serverSettingsModule = new SettingsModule();

            // set ip adress
            _serverIP = IPAddress.Parse("127.0.0.1");

            certificate = new X509Certificate2(CERTIFICATE_PATH, CERTIFICATE_PASSWORD);

            // set allowed mimetypes
            _allowedMimeTypes = _serverSettingsModule.getAllowedMIMETypes();
            _listenPort = settingsModule.getControlPort();

            _isRunning = true;
            _tcpListener = new TcpListener(_serverIP, _listenPort);
            ListenForClients();
        }

        private void ListenForClients()
        {
            _tcpListener.Start();
            Console.WriteLine("Controlserver listening on: " + _serverIP + ":" + _listenPort);

            
            while(_isRunning)
            {
                TcpClient tcpclient = _tcpListener.AcceptTcpClient();
                
                Thread clientThread = new Thread(new ParameterizedThreadStart(handleClient));
                clientThread.Start(tcpclient);
            }
        }

        private void handleClient(object client)
        {
            TcpClient tcpclient = (TcpClient)client;

            SslStream sslStream = new SslStream(tcpclient.GetStream(), false);
            sslStream.WriteTimeout = 100;
            ServicePointManager.SecurityProtocol = SecurityProtocolType.Ssl3;

            sslStream.AuthenticateAsServer(certificate);
            
            String sBuffer = readStream(sslStream);
            Console.WriteLine(sBuffer);

            if (sBuffer.Length > 3)
            {
                // Look for HTTP request
                int iStartPos = sBuffer.IndexOf("HTTP", 1);
                string sHttpVersion = null;
                if (iStartPos >= 0)
                    sHttpVersion = sBuffer.Substring(iStartPos, 8);

                string requestType = sBuffer.Substring(0, 4).Trim();
                switch (requestType)
                {
                    case "GET": handleGetRequest(sBuffer.Substring(0, iStartPos - 1), sHttpVersion, sslStream); break;
                    case "POST": ; break;
                    default: SendErrorPage(400, sHttpVersion, sslStream); return;
                }
            }

            sslStream.Close();
        }

        private String readStream(SslStream stream)
        {
            StreamReader streamReader = new StreamReader(stream);
            char[] resultBuffer = new char[2048];
            String readString = "";
            int read = -1;

            do
            {
                try
                {
                    read        = streamReader.Read(resultBuffer, 0, resultBuffer.Length);
                    readString += new String(resultBuffer).Trim('\0');
                }
                catch (Exception e)
                {
                    // uh-oh, let's break the while 
                    break;
                }

            } while (read < 3);
            
            return readString;
        }

        private void handleGetRequest(String sRequest, String sHttpVersion, SslStream sslStream)
        {
            // Replace the escaped slashes
            sRequest.Replace("\\", "/");

            if ((sRequest.IndexOf(".") < 1) && (!sRequest.EndsWith("/")))
                sRequest += "/";

            // Get the directory
            String sRequestDirectoryName = sRequest.Substring(sRequest.IndexOf("/"), sRequest.LastIndexOf("/") - 3);
            // If directory is root then give empty string
            sRequestDirectoryName = (sRequestDirectoryName.Equals("/")) ? "" : sRequestDirectoryName;

            // Add 'control' to directory name, as it's the control server
            String sDirectoryName = CombinePaths("control", sRequestDirectoryName);

            // Check if directory exists
            String localPath = getLocalPath(sDirectoryName);
            if (String.IsNullOrEmpty(localPath))
            {
                SendErrorPage(404, sHttpVersion, sslStream);
                return;
            }

            // Check if file is given, get default file if not given
            String sRequestedFile = sRequest.Substring(sRequest.LastIndexOf("/") + 1);
            if (string.IsNullOrEmpty(sRequestedFile))
            {
                List<String> defaultPages = _publicSettingsModule.getControlDefaultPage();
                foreach (String defaultPage in defaultPages)
                {
                    if (File.Exists(Path.Combine(localPath, defaultPage)))
                    {
                        sRequestedFile = defaultPage;
                        break;
                    }
                }
            }

            // Check file mimetype
            String mimeType = getMimeType(sRequestedFile);
            if (String.IsNullOrEmpty(mimeType))
            {
                SendErrorPage(404, sHttpVersion, sslStream);
                return;
            }

            // Read file with FileStream
            String sPhysicalFilePath = Path.Combine(localPath, sRequestedFile);
            FileStream fs            = null;
            if (File.Exists(sPhysicalFilePath))
            {
                fs = new FileStream(sPhysicalFilePath, FileMode.Open, FileAccess.Read, FileShare.Read);
            }
            else
            {
                SendErrorPage(404, sHttpVersion, sslStream);
                return;
            }

            // Create byteReader
            BinaryReader reader = new BinaryReader(fs);
            byte[] bResponse    = new byte[fs.Length];
            int iTotalBytes     = 0;
            int read;
            while ((read = reader.Read(bResponse, 0, bResponse.Length)) != 0)
            {
                iTotalBytes += read;
            }

            reader.Close();
            fs.Close();

            // Write data to the browser
            sendHeader(sHttpVersion, mimeType, iTotalBytes, "200 OK", sslStream);
            sendToBrowser(bResponse, sslStream);
        }

        private void sendHeader(string sHttpVersion, string sMIMEHeader, int iTotBytes, string sStatusCode, SslStream sslStream)
        {
            String sBuffer = "";

            // if Mime type is not provided set default to text/html
            if (sMIMEHeader.Length == 0)
            {
                sMIMEHeader = "text/html";  // Default Mime Type is text/html
            }

            sBuffer = sBuffer + sHttpVersion + sStatusCode + "\r\n";
            sBuffer = sBuffer + "Server: C#Server\r\n";
            sBuffer = sBuffer + "Content-Type: " + sMIMEHeader + "\r\n";
            sBuffer = sBuffer + "Accept-Ranges: bytes\r\n";
            sBuffer = sBuffer + "Content-Length: " + iTotBytes + "\r\n\r\n";

            Byte[] bSendData = Encoding.ASCII.GetBytes(sBuffer);

            sendToBrowser(bSendData, sslStream);
        }

        private void sendToBrowser(String sSendData, SslStream sslStream)
        {
            sendToBrowser(Encoding.ASCII.GetBytes(sSendData), sslStream);
        }

        private void sendToBrowser(Byte[] bSendData, SslStream sslStream)
        {
            try
            {
                sslStream.Write(bSendData);
            }
            catch (Exception e)
            {
                Console.WriteLine("Error Occurred : {0} ", e);
            }
        }

        public void SendErrorPage(int code, string sHttpVersion, SslStream sslStream)
        {
            string sErrorFolder = Path.Combine(Environment.CurrentDirectory, "Data\\Errors");
            string sErrorFile = "";
            string sErrorCode = "";

            switch (code)
            {
                case 404: sErrorFile = "404.html"; sErrorCode = "404 Not Found"; break;
                case 400: sErrorFile = "400.html"; sErrorCode = "400 Bad Request"; break;
            }

            String sErrorFilePath = Path.Combine(sErrorFolder, sErrorFile);
            StreamReader sr       = new StreamReader(sErrorFilePath);
    
            // Get the byte array 
            Byte[] bMessage = Encoding.ASCII.GetBytes(sr.ReadToEnd());

            sendHeader(sHttpVersion, "", bMessage.Length, sErrorCode, sslStream);
            sendToBrowser(bMessage, sslStream);
        }

        #region File methods

        public String CombinePaths(params string[] PathsToCombine)
        {
            string[] trimmedPaths = new string[PathsToCombine.Length];
            for(int i = 0; i < PathsToCombine.Length; i++)
            {
                trimmedPaths[i] = PathsToCombine[i].TrimStart('/');
            }
            return Path.Combine(trimmedPaths);
        }

        private String getLocalPath(String sRequestedDirectory)
        {
            String webServerRoot = _publicSettingsModule.getWebroot();

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

        private String getMimeType(String sRequestFile)
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

        private String getDefaultFileName(String sDirectoryName)
        {
            List<string> defaultPages = _publicSettingsModule.getDefaultPage();
            return "";
        }

        #endregion
    }
}
