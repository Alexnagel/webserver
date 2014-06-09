using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Webserver.Interfaces;
using Webserver.Modules;

namespace Webserver
{
    class Server
    {
        private static IPAddress _serverIP;
        private static int       _listenPort;

        private IPublicSettingsModule _publicSettingsModule;
        private IServerSettingsModule _serverSettingsModule;
        private LogModule _logModule;

        private TcpListener _tcpListener;
        private Boolean _isRunning;

        // Dictionary for all mimetypes
        private Dictionary<string, string> _allowedMimeTypes;
        public Server(IPublicSettingsModule settingsModule)
        {
            // set the settingsModules
            _publicSettingsModule = settingsModule;
            _serverSettingsModule = new SettingsModule();
            _serverIP = LocalIPAddress();
            
            // set logger
            _logModule = new LogModule(_serverIP.ToString());

            // get allowed Mimetypes
            _allowedMimeTypes = _serverSettingsModule.getAllowedMIMETypes();

            // Get port for corresponding server
            _listenPort = settingsModule.getWebPort();

            // Start the listener
            _isRunning = true;
            _tcpListener = new TcpListener(LocalIPAddress(), _listenPort);
            listenForClients();
        }

        public IPAddress LocalIPAddress()
        {
            IPHostEntry host;
            IPAddress localIP = null;
            host = Dns.GetHostEntry(Dns.GetHostName());
            foreach (IPAddress ip in host.AddressList)
            {
                if (ip.AddressFamily == AddressFamily.InterNetwork)
                {
                    localIP = ip;
                    break;
                }
            }
            return localIP;
        }

        private void listenForClients()
        {
            _tcpListener.Start();

            int webPortServer = _publicSettingsModule.getWebPort();
            Console.WriteLine("Listening on: " + _serverIP + ":" + webPortServer);

            while (_isRunning)
            {
                // Accept clients
                Socket client = _tcpListener.AcceptSocket();

                // Create client thread
                Thread clientThread = new Thread(new ParameterizedThreadStart(handleClient));
                clientThread.Start(client);
            }
        }

        private void handleClient(object client)
        {
            Socket socketClient = (Socket)client;

            if (socketClient.Connected)
            {
                _logModule.writeInfo(socketClient);
                Byte[] receivedBytes = new Byte[1024];
                int i = socketClient.Receive(receivedBytes, receivedBytes.Length, 0);

                string sBuffer = Encoding.ASCII.GetString(receivedBytes);

                // Look for HTTP request
                int iStartPos = sBuffer.IndexOf("HTTP", 1);
                string sHttpVersion = null;
                if(iStartPos >= 0)
                    sHttpVersion = sBuffer.Substring(iStartPos, 8);

                string requestType = sBuffer.Substring(0, 3);
                switch(requestType)
                {
                    case "GET": handleGetRequest(sBuffer.Substring(0,iStartPos -1), sHttpVersion, ref socketClient); break;
                    case "POS": ; break;
                    default: SendErrorPage(503, sHttpVersion, ref socketClient); return;
                }
            }
        }

        private void sendHeader(string sHttpVersion, string sMIMEHeader, int iTotBytes, string sStatusCode, ref Socket clientSocket)
        {
            String sBuffer = "";

            // if Mime type is not provided set default to text/html
            if (sMIMEHeader.Length == 0)
            {
                sMIMEHeader = "text/html";  // Default Mime Type is text/html
            }

            sBuffer = sBuffer + sHttpVersion + sStatusCode + "\r\n";
            sBuffer = sBuffer + "Server: cx1193719-b\r\n";
            sBuffer = sBuffer + "Content-Type: " + sMIMEHeader + "\r\n";
            sBuffer = sBuffer + "Accept-Ranges: bytes\r\n";
            sBuffer = sBuffer + "Content-Length: " + iTotBytes + "\r\n\r\n";

            Byte[] bSendData = Encoding.ASCII.GetBytes(sBuffer);

            sendToBrowser(bSendData, ref clientSocket);
        }

        private void sendToBrowser(String sSendData, ref Socket clientSocket)
        {
            sendToBrowser(Encoding.ASCII.GetBytes(sSendData), ref clientSocket);
        }

        private void sendToBrowser(Byte[] bSendData, ref Socket clientSocket)
        {
            int numBytes = 0;
            try
            {
                if (clientSocket.Connected)
                {
                    if ((numBytes = clientSocket.Send(bSendData, bSendData.Length, 0)) == -1)
                        Console.WriteLine("Socket Error cannot Send Packet");
                    else
                        Console.WriteLine("No. of bytes send {0}", numBytes);
                }
                else
                    Console.WriteLine("Connection Dropped....");
            }
            catch (Exception e)
            {
                Console.WriteLine("Error Occurred : {0} ", e);
            }
        }

        public void SendErrorPage(int code, string sHttpVersion, ref Socket clientSocket)
        {
            string eMessage = "";

            switch(code)
            {
                case 404: eMessage = "<h2>404 Page Not Found</h2>"; break;
                case 503: eMessage = "<h2>This method isn't supported</h2>"; break;
            }

            // Get the byte array 
            Byte[] bMessage = Encoding.ASCII.GetBytes(eMessage);
            
            sendHeader(sHttpVersion, "", bMessage.Length, " 200 OK", ref clientSocket);
            sendToBrowser(bMessage, ref clientSocket);
            clientSocket.Close();
        }

        private void handleGetRequest(String sRequest, String sHttpVersion, ref Socket clientSocket)
        {
            sRequest.Replace("\\", "/");

            if((sRequest.IndexOf(".") < 1) && (!sRequest.EndsWith("/")))
                sRequest += "/";

            String sDirectoryName = sRequest.Substring(sRequest.IndexOf("/"), sRequest.LastIndexOf("/") - 3);
            String sRequestedFile = sRequest.Substring(sRequest.LastIndexOf("/") + 1);

            Console.WriteLine(sDirectoryName);
            Console.WriteLine(sRequestedFile);

            // Check if localpath exists
            String localPath = getLocalPath(sDirectoryName);
            if (String.IsNullOrEmpty(localPath))
            {
                SendErrorPage(404, sHttpVersion, ref clientSocket);
                return;
            }

            // Check mimetype
            String mimeType = getMimeType(sRequestedFile);
            if (String.IsNullOrEmpty(mimeType))
            {
                SendErrorPage(404, sHttpVersion, ref clientSocket);
                return;
            }

            // File to bytes
            int iTotalBytes = 0;
            String sResponse = "";
            String sPhysicalFilePath = Path.Combine(localPath, sRequestedFile);

            FileStream fs = new FileStream(sPhysicalFilePath, FileMode.Open, FileAccess.Read, FileShare.Read);

            // Create byteReader
            BinaryReader reader = new BinaryReader(fs);
            byte[] bytes = new byte[fs.Length];
            int read;
            while((read = reader.Read(bytes,0, bytes.Length)) != 0)
            {
                sResponse += Encoding.ASCII.GetString(bytes, 0, read);
                iTotalBytes += read;
            }

            reader.Close();
            fs.Close();

            sendHeader(sHttpVersion, mimeType, iTotalBytes, "200 OK", ref clientSocket);
            sendToBrowser(sResponse, ref clientSocket);
        }

#region File methods
        
        private String getLocalPath(String sRequestedDirectory)
        {
            String webServerRoot = _publicSettingsModule.getWebroot();

            // Remove spaces and lower case
            sRequestedDirectory.Trim();
            sRequestedDirectory = sRequestedDirectory.ToLower();

            String localPath = webServerRoot;
            if(!sRequestedDirectory.Equals("/"))
                localPath = Path.Combine(webServerRoot,sRequestedDirectory);

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