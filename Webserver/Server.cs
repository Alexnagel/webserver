using System;
using System.Collections.Generic;
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
        private static IPAddress serverIP = Dns.GetHostEntry("localhost").AddressList[0];
        private IPublicSettingsModule settingsModule;

        private TcpListener tcpListener;
        private Boolean isRunning;

        public Server(IPublicSettingsModule settingsModule, Boolean isControlServer)
        {
            // set the settingsModule
            this.settingsModule = settingsModule;

            // Get port for corresponding server
            int listenPort = 0;
            if (isControlServer)
                listenPort = settingsModule.getControlPort();
            else
                listenPort = settingsModule.getWebPort();

            // Start the listener
            isRunning = true;
            tcpListener = new TcpListener(serverIP, listenPort);
            listenForClients();
        }

        private void listenForClients()
        {
            tcpListener.Start();

            while(isRunning)
            {
                // Accept clients
                TcpClient client = tcpListener.AcceptTcpClient();
                
                // Create client thread
                Thread clientThread = new Thread(new ParameterizedThreadStart(handleClient));
                clientThread.Start();
            }
        }

        private void handleClient(object client)
        {
            TcpClient tcpClient = (TcpClient)client;
            NetworkStream clientStream = tcpClient.GetStream();
            byte[] message = new byte[4096];
            int bytesRead;
            while (true)
            {
                bytesRead = 0;
                try
                {
                    //blocks until a client sends a message
                    bytesRead = clientStream.Read(message, 0, 4096);
                }
                catch
                {
                    //a socket error has occured
                    break;
                }
                if (bytesRead == 0)
                {
                    //the client has disconnected from the server
                    break;
                }
                //message has successfully been received
                ASCIIEncoding encoder = new ASCIIEncoding();
                Console.WriteLine(encoder.GetString(message, 0, bytesRead));
            }
        }
    }
}
