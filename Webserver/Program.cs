using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Webserver.Interfaces;
using Webserver.Modules;

namespace Webserver
{
    class Program
    {
        static void Main(string[] args)
        {
            IPublicSettingsModule settingsModule = new SettingsModule();

            Server webServer;
            //Server controlServer;

            Thread webServerThread = new Thread(() => { webServer = new Server(settingsModule, false); } );
            //Thread controlServerThread = new Thread(() => { controlServer = new Server(settingsModule, true); });
            webServerThread.Start();
            //controlServerThread.Start();
        }
    }
}
