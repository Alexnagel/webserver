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
        private static IPublicSettingsModule _settingsModule;
        private static SessionModule         _sessionModule;
        private static MySqlModule           _mysqlModule;
        private static LogModule             _logModule;

        private static Thread _controlServerThread;
        private static Thread _webServerThread;
        private static Thread _loggerThread;

        static void Main(string[] args)
        {
            Console.CancelKeyPress += delegate
            {
                Environment.Exit(0);
            };

            // Set modules
            _settingsModule = new SettingsModule();
            _mysqlModule    = new MySqlModule();
            _sessionModule  = new SessionModule(_mysqlModule);
            _logModule      = new LogModule();

            // Add settings event listener
            _settingsModule.SettingsUpdated += restartServers;

            initServers(_settingsModule);

            Console.WriteLine("To exit press ctrl+c");
        }

        private static void initServers(IPublicSettingsModule settingsModule)
        {
            //Create threads for each server
            _controlServerThread = new Thread(() => new ControlServer(_settingsModule, _sessionModule, _logModule));
            _controlServerThread.Start();

            _webServerThread = new Thread(() => new Server(_settingsModule, _logModule));
            _webServerThread.Start();

            //_loggerThread = new Thread(() => new Logger());
        }

        private static void restartServers(object sender, Boolean settingsSuccess)
        {
            Console.WriteLine("Restarting Servers");

            // Restart servers to reflect settings
            initServers(_settingsModule);
        }
    }
}
