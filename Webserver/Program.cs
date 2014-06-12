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
            Console.CancelKeyPress += delegate
            {
                Environment.Exit(0);
            };

            IPublicSettingsModule settingsModule = new SettingsModule();

            Server webServer = new Server(settingsModule);
            //Server controlServer;

            //Thread webServerThread = new Thread(() => { webServer = new Server(settingsModule); } );
            //Thread controlServerThread = new Thread(() => { controlServer = new Server(settingsModule, true); });
            //webServerThread.Start();
            //controlServerThread.Start();
        }
    }
}
