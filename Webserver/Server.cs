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

namespace Webserver
{
    class Server
    {
        private static IPAddress _serverIP;
        private static int       _listenPort;

        private IPublicSettingsModule _settingsModule;

        private TcpListener _tcpListener;
        private Boolean _isRunning;

        public Server(IPublicSettingsModule settingsModule)
        {
            // set the settingsModule
            _settingsModule = settingsModule;
            _serverIP = LocalIPAddress();

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

            Console.WriteLine("Listening on: " + _serverIP + ":8000");

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
                Byte[] receivedBytes = new Byte[1024];
                int i = socketClient.Receive(receivedBytes, receivedBytes.Length, 0);

                string sBuffer = Encoding.ASCII.GetString(receivedBytes);

                // Look for HTTP request
                int iStartPos = sBuffer.IndexOf("HTTP", 1);
                string sHttpVersion = sBuffer.Substring(iStartPos, 8);

                string requestType = sBuffer.Substring(0, 3);
                switch(requestType)
                {
                    case "GET": ; break;
                    case "POST": ; break;
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
        }
    }
}