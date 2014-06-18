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
    class Server : AbstractServer
    {
        private const int INIT_THREADS = 20;
        private const int MAX_THREADS = 20;
        private const String DIRECTORY_BROWSING_TEMPLATE = "Data\\Templates\\Directorybrowsing.html";
        private const String DIRECTORY_LINK_TEMPLATE = "Data\\Templates\\DirectoryLink.html";

        private static Semaphore _connectionSemaphore;
        private static IPAddress _serverIP;
        private static int       _listenPort;

        private IPublicSettingsModule _publicSettingsModule;
        private IServerSettingsModule _serverSettingsModule;
        private FileModule            _fileModule;
        private LogModule             _logModule;

        private TcpListener _tcpListener;
        private Boolean     _isRunning;

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

            // Set the filemodule
            _fileModule = new FileModule();
            
            // set logger
            _logModule = new LogModule(_serverIP.ToString());

            // get allowed Mimetypes
            _allowedMimeTypes = _serverSettingsModule.getAllowedMIMETypes();

            // Get port for corresponding server
            _listenPort = settingsModule.GetWebPort();

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
            // Use to write to the browser, needed for AbstractServer
            // BufferedStream because it's 5x faster, don't ask me why
            BufferedStream stream = new BufferedStream(new NetworkStream(socketClient));

            Stopwatch stopWatch = new Stopwatch();
            stopWatch.Start();
            if (socketClient.Connected)
            {
                Byte[] receivedBytes = new Byte[1024];
                Byte[] receivedBytes2 = new Byte[1024];
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
                        case "GET": handleGetRequest(sBuffer.Substring(0, iStartPos - 1), sHttpVersion, stream); break;
                        case "POST": ; break;
                        default: SendErrorPage(400, sHttpVersion, stream); return;
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

        private void handleGetRequest(String sRequest, String sHttpVersion, Stream clientSocket)
        {
            sRequest.Replace("\\", "/");

            if((sRequest.IndexOf(".") < 1) && (!sRequest.EndsWith("/")))
                sRequest += "/";

            String sDirectoryName = sRequest.Substring(sRequest.IndexOf("/"), sRequest.LastIndexOf("/") - 3);
            String sRequestedFile = sRequest.Substring(sRequest.LastIndexOf("/") + 1);

            Console.WriteLine(sDirectoryName);
            Console.WriteLine(sRequestedFile);

            // Check if localpath exists
            String sLocalPath = _fileModule.GetLocalPath(sDirectoryName);
            if (String.IsNullOrEmpty(sLocalPath))
            {
                SendErrorPage(404, sHttpVersion, clientSocket);
                return;
            }

            // Check if file is given, get default file if not given
            if (string.IsNullOrEmpty(sRequestedFile))
            {
                sRequestedFile = _fileModule.GetDefaultPage(sLocalPath);
            }

            if (String.IsNullOrWhiteSpace(sRequestedFile) && _publicSettingsModule.GetAllowedDirectoryBrowsing())
            {
                createDirectoryBrowsing(sLocalPath, sDirectoryName, sHttpVersion, clientSocket);
            }
            else
                SendErrorPage(404, sHttpVersion, clientSocket);

            // Check file mimetype
            String mimeType = _fileModule.GetMimeType(sRequestedFile);
            if (String.IsNullOrEmpty(mimeType))
            {
                SendErrorPage(404, sHttpVersion, clientSocket);
                return;
            }

            // File to bytes
            String sPhysicalFilePath = Path.Combine(sLocalPath, sRequestedFile);
            byte[] bFileBytes = _fileModule.FileToBytes(sPhysicalFilePath);

            // save get info
            String webServerRoot = _publicSettingsModule.GetWebroot();
            //_logModule.writeInfo(clientSocket, sDirectoryName, sRequestedFile, webServerRoot);

            // Write data to the browser
            SendHeader(sHttpVersion, mimeType, bFileBytes.Length, "200 OK", clientSocket);
            SendToBrowser(bFileBytes, clientSocket);
        }

        private void createDirectoryBrowsing(String sDirectoryPath, String sRelativeDir, String sHttpVersion, Stream clientSocket)
        {
            String sDirBrowsingPath = Path.Combine(Environment.CurrentDirectory, DIRECTORY_BROWSING_TEMPLATE);
            String sDirLinkPath     = Path.Combine(Environment.CurrentDirectory, DIRECTORY_LINK_TEMPLATE);
            String sDirBrowsingTemplate = "";
            String sDirLinkTemplate = "";
            String sLinks = "";

            using(StreamReader sr = new StreamReader(sDirBrowsingPath))
                 sDirBrowsingTemplate = sr.ReadToEnd();

            using (StreamReader sr = new StreamReader(sDirLinkPath))
                sDirLinkTemplate = sr.ReadToEnd();

            Dictionary<String, String> dDirFiles = _fileModule.GetAllFilesFromDirectory(sRelativeDir);
            foreach (KeyValuePair<String, String> item in dDirFiles)
                sLinks += sDirLinkTemplate.Replace("{{link}}", item.Value).Replace("{{directory}}", item.Key) + "\n";

            sDirBrowsingTemplate = sDirBrowsingTemplate.Replace("{{folder}}", sRelativeDir).Replace("{{links}}", sLinks);

            byte[] bFileBytes = Encoding.ASCII.GetBytes(sDirBrowsingTemplate);
            SendHeader(sHttpVersion, "", bFileBytes.Length, "200 OK", clientSocket);
            SendToBrowser(bFileBytes, clientSocket);
        }
    }
}