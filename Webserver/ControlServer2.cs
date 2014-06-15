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
    class ControlServer2
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

        private int bytes = -1;

        public ControlServer2(IPublicSettingsModule settingsModule)
        {
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
                Socket client = _tcpListener.AcceptSocket();

                Thread clientThread = new Thread(new ParameterizedThreadStart(handleClient));
                clientThread.Start(client);
            }
        }

        private void handleClient(object client)
        {
            Socket socketClient = (Socket)client;
            if (socketClient.Connected)
            {
                SslStream sslStream = new SslStream(new NetworkStream(socketClient), false);
                sslStream.ReadTimeout = 100;
                sslStream.WriteTimeout = 100;

                byte[] buffer = new byte[2048];
                StringBuilder messageData = new StringBuilder();

                try
                {
                    sslStream.AuthenticateAsServer(certificate, false, SslProtocols.Ssl3, true);

                    do
                    {
                        bytes = sslStream.Read(buffer, 0, buffer.Length);
                        Decoder decoder = Encoding.UTF8.GetDecoder();
                        char[] chars = new char[decoder.GetCharCount(buffer, 0, bytes)];

                        decoder.GetChars(buffer, 0, bytes, chars, 0);
                        messageData.Append(chars);
                        Console.WriteLine(messageData.ToString());

                        String sBuffer = messageData.ToString();
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
                                case "GET": handleGetRequest(sBuffer.Substring(0, iStartPos - 1), sHttpVersion, ref socketClient, bytes, sslStream); break;
                                case "POST": ; break;
                                default: SendErrorPage(400, sHttpVersion, ref socketClient, bytes, sslStream); return;
                            }

                        }

                        // Check for EOF. 
                        if (messageData.ToString().IndexOf("\r\n") != -1)
                        {
                            break;
                        }
                    }
                    while (bytes != 0);
                }
                catch (AuthenticationException ex)
                {
                    Console.WriteLine(ex);
                }
                catch (IOException e)
                {
                    Console.WriteLine(e);
                }
                finally
                {
                    sslStream.Close();
                    socketClient.Close();
                }
            }
        }

        private void sendHeader(string sHttpVersion, string sMIMEHeader, int iTotBytes, string sStatusCode, ref Socket clientSocket, int bytes,SslStream sslStream)
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

            sendToBrowser(bSendData, ref clientSocket, bytes, sslStream);
        }

        private void sendToBrowser(String sSendData, ref Socket clientSocket,int bytes,SslStream sslStream)
        {
            sendToBrowser(Encoding.ASCII.GetBytes(sSendData), ref clientSocket, bytes, sslStream);
        }

        private void sendToBrowser(Byte[] bSendData, ref Socket clientSocket, int bytes, SslStream sslStream)
        {
            int numBytes = bytes;

            try
            {
                if (clientSocket.Connected)
                {
                    
                    sslStream.Write(bSendData);
                    sslStream.Flush();

                    if(numBytes == -1)
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

        public void SendErrorPage(int code, string sHttpVersion, ref Socket clientSocket, int bytes, SslStream sslStream)
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
            StreamReader sr = new StreamReader(sErrorFilePath);

            // Get the byte array 
            Byte[] bMessage = Encoding.ASCII.GetBytes(sr.ReadToEnd());

            sendHeader(sHttpVersion, "", bMessage.Length, sErrorCode, ref clientSocket, bytes, sslStream);
            sendToBrowser(bMessage, ref clientSocket, bytes, sslStream);
        }

        private void handleGetRequest(String sRequest, String sHttpVersion, ref Socket clientSocket, int bytes, SslStream sslStream)
        {
            int newByte = bytes;
            sRequest.Replace("\\", "/");

            if ((sRequest.IndexOf(".") < 1) && (!sRequest.EndsWith("/")))
                sRequest += "/";

            String sDirectoryName = sRequest.Substring(sRequest.IndexOf("/"), sRequest.LastIndexOf("/") - 3);
            String sRequestedFile = sRequest.Substring(sRequest.LastIndexOf("/") + 1);

            Console.WriteLine(sDirectoryName);
            Console.WriteLine(sRequestedFile);

            // Check if localpath exists
            String localPath = getLocalPath(sDirectoryName);
            if (String.IsNullOrEmpty(localPath))
            {
                SendErrorPage(404, sHttpVersion, ref clientSocket, bytes,sslStream);
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
            int newBytes = bytes;
            if (String.IsNullOrEmpty(mimeType))
            {
                SendErrorPage(404, sHttpVersion, ref clientSocket, newBytes,sslStream);
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
                SendErrorPage(404, sHttpVersion, ref clientSocket,bytes,sslStream);
                return;
            }

            // Create byteReader
            BinaryReader reader = new BinaryReader(fs);
            byte[] bBytes = new byte[fs.Length];
            int read;
            while ((read = reader.Read(bBytes, 0, bBytes.Length)) != 0)
            {
                sResponse += Encoding.ASCII.GetString(bBytes, 0, read);
                iTotalBytes += read;
            }

            reader.Close();
            fs.Close();

            // save get info
            String webServerRoot = _publicSettingsModule.getWebroot();
            //_logModule.writeInfo(ref clientSocket, sDirectoryName, sRequestedFile, webServerRoot);

            // Write data to the browser
            sendHeader(sHttpVersion, mimeType, iTotalBytes, "200 OK", ref clientSocket,bytes,sslStream);
            sendToBrowser(sResponse, ref clientSocket, bytes,sslStream);
        }

        #region File methods

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
