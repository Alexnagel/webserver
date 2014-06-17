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
    class ControlServer : AbstractServer
    {
        private const string CERTIFICATE_PATH = @"Data\webserverCA.pfx";
        private const string CERTIFICATE_PASSWORD = "webserver";

        private static IPAddress _serverIP;
        private static int       _listenPort;

        // settings modules
        private IPublicSettingsModule _publicSettingsModule;
        private IServerSettingsModule _serverSettingsModule;

        // File Module
        private FileModule _fileModule;

        // Dictionary for all mimetypes
        private Dictionary<string, string> _allowedMimeTypes;

        private TcpListener _tcpListener;
        private Boolean     _isRunning;

        private X509Certificate certificate;

        private MySqlModule mySqlModule;

        public ControlServer(IPublicSettingsModule settingsModule)
        {
            // Connect DB
            mySqlModule = new MySqlModule();

            // set the settingsmodules
            _publicSettingsModule = settingsModule;
            _serverSettingsModule = new SettingsModule();

            // Set the filemodule
            _fileModule = new FileModule();

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


            while (_isRunning)
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
                    case "POST": handlePostRequest(sBuffer, sHttpVersion, sslStream); break;
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
                    read = streamReader.Read(resultBuffer, 0, resultBuffer.Length);
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
            String sDirectoryName = _fileModule.CombinePaths("control", sRequestDirectoryName);

            // Check if localPath exists
            String sLocalPath = _fileModule.GetLocalPath(sDirectoryName);
            if (String.IsNullOrEmpty(sLocalPath))
            {
                SendErrorPage(404, sHttpVersion, sslStream);
                return;
            }

            // Check if file is given, get default file if not given
            String sRequestedFile = sRequest.Substring(sRequest.LastIndexOf("/") + 1);
            if (string.IsNullOrEmpty(sRequestedFile))
            {
                sRequestedFile = _fileModule.GetControlDefaultPage(sLocalPath);
            }

            // Check file mimetype
            String mimeType = _fileModule.GetMimeType(sRequestedFile);
            if (String.IsNullOrEmpty(mimeType))
            {
                SendErrorPage(404, sHttpVersion, sslStream);
                return;
            }

            // File to bytes
            String sPhysicalFilePath = Path.Combine(sLocalPath, sRequestedFile);
            byte[] bFileBytes = _fileModule.FileToBytes(sPhysicalFilePath);

            // Write data to the browser
            SendHeader(sHttpVersion, mimeType, bFileBytes.Length, "200 OK", sslStream);
            SendToBrowser(bFileBytes, sslStream);
        }

        private void handlePostRequest(String sRequest, String sHttpVersion, SslStream sslStream)
        {
            // Get the post data content length
            int iStartPos      = sRequest.IndexOf("Content-Length: ") + 16;
            int iEndPos        = sRequest.IndexOf('\r', iStartPos);
            int iContentLength = int.Parse(sRequest.Substring(iStartPos, iEndPos - iStartPos));

            // Get the post data string
            String sPostData = sRequest.Substring(sRequest.LastIndexOf('\n') + 1, iContentLength);
            
            // Split post data and create dictionary of that data, key = input name, value = input value
            String[] saPostData = sPostData.Split('&');
            Dictionary<String, String> dPostData = new Dictionary<String, String>();
            for (int i = 0; i < saPostData.Length; i++ )
            {
                String[] saSeperateData = saPostData[i].Split('=');
                dPostData.Add(saSeperateData[0], saSeperateData[1]);
            }
            
            // Get post method
            String sPostMethod = "";
            int iPostStartPos = sRequest.IndexOf("HTTP", 1);
            sRequest = sRequest.Substring(0, iPostStartPos - 1);

            // Replace the escaped slashes
            sRequest.Replace("\\", "/");

            if ((sRequest.IndexOf(".") < 1) && (!sRequest.EndsWith("/")))
                sRequest += "/";

            String sRequestedFile = sRequest.Substring(sRequest.LastIndexOf("/") + 1);
            if (String.IsNullOrWhiteSpace(sRequestedFile))
            {
                sPostMethod = "login";
            }
            else
            {
                sPostMethod = Path.GetFileNameWithoutExtension(sRequestedFile);
            }

            handlePostMethod(sPostMethod, dPostData, sHttpVersion, sslStream);
        }

        private void handlePostMethod(String sPostMethod, Dictionary<String, String> dPostData, String sHttpVersion, SslStream sslStream)
        {
            switch(sPostMethod)
            {
                case "login": loginMethod(dPostData, sHttpVersion, sslStream); break;
                default: SendErrorPage(404, sHttpVersion, sslStream); break;
            }
        }

        private void loginMethod(Dictionary<String, String> dPostData, String sHttpVersion, SslStream sslStream)
        {

        }
    }
}