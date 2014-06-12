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
            
            //Create threads for each server
            Thread controlServerThread = new Thread(() => new ControlServer2(settingsModule));
            controlServerThread.Start();
            Thread webServerThread = new Thread(() => new Server(settingsModule));
            webServerThread.Start();
            
        }
    }
}
