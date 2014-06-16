using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Security;
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
        private const int INIT_THREADS = 20;
        private const int MAX_THREADS = 20;

        private static Semaphore _connectionSemaphore;
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
            // set the semaphore
            _connectionSemaphore = new Semaphore(INIT_THREADS, MAX_THREADS);

            // set the settingsModules
            _publicSettingsModule = settingsModule;
            _serverSettingsModule = new SettingsModule();
            _serverIP = IPAddress.Parse("127.0.0.1");
            
            // set logger
            _logModule = new LogModule(_serverIP.ToString());

            // get allowed Mimetypes
            _allowedMimeTypes = _serverSettingsModule.getAllowedMIMETypes();

            // Get port for corresponding server
            _listenPort = settingsModule.getWebPort();

            // Start the listener
            _isRunning = true;
            _tcpListener = new TcpListener(_serverIP, _listenPort);
            listenForClients();
        }

        private void listenForClients()
        {
            _tcpListener.Start();

            // Write in console program has started
            Console.WriteLine("Webserver listening on: " + _serverIP + ":" + _listenPort);

            while (_isRunning)
            {
                _connectionSemaphore.WaitOne();
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
            Stopwatch stopWatch = new Stopwatch();
            stopWatch.Start();
            if (socketClient.Connected)
            {
                Byte[] receivedBytes = new Byte[1024];
                int i = socketClient.Receive(receivedBytes, receivedBytes.Length, 0);

                string sBuffer = Encoding.ASCII.GetString(receivedBytes);
                sBuffer = sBuffer.Trim('\0');

                if (sBuffer.Length > 0)
                {
                    // Look for HTTP request
                    int iStartPos = sBuffer.IndexOf("HTTP", 1);
                    string sHttpVersion = null;
                    if (iStartPos >= 0)
                        sHttpVersion = sBuffer.Substring(iStartPos, 8);

                    string requestType = sBuffer.Substring(0, 4).Trim();
                    
                    
                    switch (requestType)
                    {
                        case "GET": handleGetRequest(sBuffer.Substring(0, iStartPos - 1), sHttpVersion, ref socketClient); break;
                        case "POST": ; break;
                        default: SendErrorPage(400, sHttpVersion, ref socketClient); return;
                    }
                }
                stopWatch.Stop();
                TimeSpan ts = stopWatch.Elapsed;
                string newDate = DateTime.Now.ToString("yyyy-MM-dd HH':'mm':'ss");
                string elapsedTime = ts.Milliseconds.ToString();
                Console.WriteLine("LOG - IP : " + _serverIP + ", Date : " + newDate + ", responsetime : " + elapsedTime + ", URL : ");
            }
            _connectionSemaphore.Release();
        }

        private void sendHeader(string sHttpVersion, string sMIMEHeader, int iTotBytes, string sStatusCode, ref Socket clientSocket)
        {
            String sBuffer = "";

            // if Mime type is not provided set default to text/html
            if (sMIMEHeader.Length == 0)
            {
                sMIMEHeader = "text/html";  // Default Mime Type is text/html
            }

            sBuffer = sBuffer + sHttpVersion + " " + sStatusCode + "\r\n";
            sBuffer = sBuffer + "Server: C#Server\r\n";
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
                    {
                        Console.WriteLine("No. of bytes send {0}", numBytes);
                    }
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
            string sErrorFolder = Path.Combine(Environment.CurrentDirectory, "Data\\Errors");
            string sErrorFile = "";
            string sErrorCode = "";

            switch(code)
            {
                case 404: sErrorFile = "404.html"; sErrorCode = "404 Not Found"; break;
                case 400: sErrorFile = "400.html"; sErrorCode = "400 Bad Request"; break;
            }

            String sErrorFilePath = Path.Combine(sErrorFolder, sErrorFile);
            StreamReader sr = new StreamReader(sErrorFilePath);

            // Get the byte array 
            Byte[] bMessage = Encoding.ASCII.GetBytes(sr.ReadToEnd());
            
            sendHeader(sHttpVersion, "", bMessage.Length, sErrorCode, ref clientSocket);
            sendToBrowser(bMessage, ref clientSocket);
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

            // Check if file is given, get default file if not given
            if (string.IsNullOrEmpty(sRequestedFile))
            {
                List<String> defaultPages = _publicSettingsModule.getDefaultPage();
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
                SendErrorPage(404, sHttpVersion, ref clientSocket);
                return;
            }

            // File to bytes
            int iTotalBytes = 0;
            String sResponse = "";
            String sPhysicalFilePath = Path.Combine(localPath, sRequestedFile);

            FileStream fs = null;
            if (File.Exists(sPhysicalFilePath))
            {
                fs = new FileStream(sPhysicalFilePath, FileMode.Open, FileAccess.Read, FileShare.Read);
            }
            else
            {
                SendErrorPage(404, sHttpVersion, ref clientSocket);
                return;
            }

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

            // save get info
            String webServerRoot = _publicSettingsModule.getWebroot();
            _logModule.writeInfo(ref clientSocket, sDirectoryName, sRequestedFile, webServerRoot);

            // Write data to the browser
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

#endregion

    }
}