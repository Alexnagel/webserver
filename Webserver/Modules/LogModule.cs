using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.Sockets;
using System.Text;
using System.Threading.Tasks;

namespace Webserver.Modules
{
    class LogModule
    {
        private String _serverIP;

        public LogModule()
        {
            this._serverIP = "";
        }

        public void writeInfo(ref Socket client, String sDirectoryName, String sRequestedFile, String webserverRoot)
        {
            Socket socketClient = (Socket)client;
            Console.WriteLine("LOG : " + socketClient.AddressFamily + " " + socketClient.LocalEndPoint + sDirectoryName + sRequestedFile);
            //StreamWriter sw = new StreamWriter(webserverRoot + @"\Log\log.txt", true);
            //String line = "LOG : " + DateTime.Now + " " + client.LocalEndPoint + sDirectoryName + sRequestedFile;
            //sw.WriteLine(line);
            //sw.Close();
        }

        public List<String> GetAllLogs()
        {
            return new List<string>();
        }
    }
}
