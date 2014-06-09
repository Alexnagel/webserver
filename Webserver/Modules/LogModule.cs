using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Sockets;
using System.Text;
using System.Threading.Tasks;

namespace Webserver.Modules
{
    class LogModule
    {
        private String _serverIP;

        public LogModule(String serverIP)
        {
            this._serverIP = serverIP;
        }

        public void writeInfo(Socket client)
        {
            Socket socketClient = (Socket)client;
            
        }
    }
}
